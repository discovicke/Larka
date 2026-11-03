using System.Net.Http.Headers;
using Backend.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Storage;

// Supabase Storage REST API. Service role-nyckeln går förbi Supabases row level security, så alla
// åtkomstkontroller görs i våra services innan en länk skapas.
public class SupabaseFileStorage(HttpClient http, IOptions<SupabaseStorageOptions> options) : IFileStorage
{
    private readonly SupabaseStorageOptions _options = options.Value;

    private record SignRequest(int ExpiresIn);

    private record SignManyRequest(int ExpiresIn, List<string> Paths);

    private record CopyRequest(string BucketId, string SourceKey, string DestinationKey);

    private record DeleteRequest(List<string> Prefixes);

    private record SignResponse(string SignedUrl);

    private record SignManyResponse(string? Path, string? SignedUrl, string? Error);

    public async Task<string> UploadAsync(string folder, Stream content, string contentType, CancellationToken ct)
    {
        var key = $"{folder}/{Guid.NewGuid()}";
        using var body = new StreamContent(content);
        body.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        using var response = await http.PostAsync(ObjectPath(key), body, ct);
        response.EnsureSuccessStatusCode();
        return key;
    }

    public async Task<string> GetSignedUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(
            $"object/sign/{_options.Bucket}/{key}", new SignRequest((int)expiresIn.TotalSeconds), ct);
        response.EnsureSuccessStatusCode();

        var signed = await response.Content.ReadFromJsonAsync<SignResponse>(ct)
            ?? throw new InvalidOperationException("Supabase returned no signed URL.");
        return Absolute(signed.SignedUrl);
    }

    // Ett anrop för alla filer i stället för ett per fil. download= gör att Supabase skickar filen med
    // Content-Disposition och det riktiga namnet.
    public async Task<IReadOnlyDictionary<string, string>> GetDownloadUrlsAsync(
        IReadOnlyCollection<StoredFile> files, TimeSpan expiresIn, CancellationToken ct)
    {
        if (files.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var names = files.DistinctBy(file => file.Key).ToDictionary(file => file.Key, file => file.FileName);
        using var response = await http.PostAsJsonAsync(
            $"object/sign/{_options.Bucket}",
            new SignManyRequest((int)expiresIn.TotalSeconds, names.Keys.ToList()),
            ct);
        response.EnsureSuccessStatusCode();

        var signed = await response.Content.ReadFromJsonAsync<List<SignManyResponse>>(ct) ?? [];
        var urls = new Dictionary<string, string>();
        foreach (var item in signed)
        {
            if (item.Path is null || item.SignedUrl is null || !names.TryGetValue(item.Path, out var name))
            {
                throw new FileNotFoundException(item.Error ?? "Supabase could not sign the file.", item.Path);
            }

            urls[item.Path] = $"{Absolute(item.SignedUrl)}&download={Uri.EscapeDataString(name)}";
        }

        return urls.Count == names.Count
            ? urls
            : throw new InvalidOperationException("Supabase did not sign every file.");
    }

    public async Task<string> CopyAsync(string sourceKey, string folder, CancellationToken ct)
    {
        var key = $"{folder}/{Guid.NewGuid()}";
        using var response = await http.PostAsJsonAsync(
            "object/copy", new CopyRequest(_options.Bucket, sourceKey, key), ct);
        response.EnsureSuccessStatusCode();
        return key;
    }

    // Massborttagningen svarar 200 även när filen redan är borta, till skillnad från DELETE på ett enskilt objekt.
    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"object/{_options.Bucket}")
        {
            Content = JsonContent.Create(new DeleteRequest([key]))
        };
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private string ObjectPath(string key) => $"object/{_options.Bucket}/{key}";

    // Supabase svarar med en sökväg som börjar på /object/sign/..., relativ till /storage/v1/.
    private string Absolute(string signedUrl) => new Uri(http.BaseAddress!, signedUrl.TrimStart('/')).ToString();
}
