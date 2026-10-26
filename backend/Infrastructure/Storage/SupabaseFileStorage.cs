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

    private record SignResponse(string SignedUrl);

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
        return new Uri(http.BaseAddress!, signed.SignedUrl.TrimStart('/')).ToString();
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        using var response = await http.DeleteAsync(ObjectPath(key), ct);
        response.EnsureSuccessStatusCode();
    }

    private string ObjectPath(string key) => $"object/{_options.Bucket}/{key}";
}
