using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Backend.Application.Abstractions;
using Backend.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Storage;

// Filerna på disk, för en skola som kör Lärka själv utan Supabase. Varje fil sparas under Storage:Disk:Path med sin
// innehållstyp bredvid (.type). Länkarna är som Supabases: /files/{nyckel}?expires=...&sig=..., signerade med en
// nyckel som härleds ur JWT-nyckeln och giltiga en kort stund. Appen själv lämnar ut filen (FileEndpoints).
public partial class DiskFileStorage(
    IOptions<DiskStorageOptions> options,
    IOptions<JwtOptions> jwt,
    TimeProvider time) : IFileStorage
{
    public const string RoutePrefix = "/files";

    private readonly string _root = Path.GetFullPath(options.Value.Path);
    private readonly byte[] _signingKey = SHA256.HashData(Encoding.UTF8.GetBytes("larka-files:" + jwt.Value.Key));

    public async Task<string> UploadAsync(string folder, Stream content, string contentType, CancellationToken ct)
    {
        var key = $"{folder}/{Guid.NewGuid()}";
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file, ct);
        }

        await File.WriteAllTextAsync(path + ".type", contentType, ct);
        return key;
    }

    public Task<string> GetSignedUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct)
    {
        if (!File.Exists(PathOf(key)))
        {
            throw new FileNotFoundException("No such file in storage.", key);
        }

        var expires = time.GetUtcNow().Add(expiresIn).ToUnixTimeSeconds();
        return Task.FromResult($"{RoutePrefix}/{key}?expires={expires}&sig={Sign(key, expires)}");
    }

    public async Task<string> CopyAsync(string sourceKey, string folder, CancellationToken ct)
    {
        var source = PathOf(sourceKey);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("No such file in storage.", sourceKey);
        }

        var key = $"{folder}/{Guid.NewGuid()}";
        var target = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target);
        await File.WriteAllTextAsync(target + ".type", await ReadTypeAsync(source, ct), ct);
        return key;
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = PathOf(key);
        File.Delete(path);
        File.Delete(path + ".type");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Filen bakom en signerad länk, eller null när länken är ogiltig, har gått ut eller filen saknas. Samma svar i
    /// alla fallen, så att ingen kan pröva sig fram till vilka filer som finns.
    /// </summary>
    public async Task<(string Path, string ContentType)?> OpenAsync(
        string key,
        long expires,
        string signature,
        CancellationToken ct)
    {
        if (!IsValidKey(key) || expires < time.GetUtcNow().ToUnixTimeSeconds())
        {
            return null;
        }

        var expected = Encoding.ASCII.GetBytes(Sign(key, expires));
        if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(signature)))
        {
            return null;
        }

        var path = PathOf(key);
        return File.Exists(path)
            ? (path, await ReadTypeAsync(path, ct))
            : null;
    }

    private string Sign(string key, long expires)
    {
        var hash = HMACSHA256.HashData(_signingKey, Encoding.UTF8.GetBytes($"{key}\n{expires}"));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // Nycklarna skapas av oss (mapp/guid), men en nyckel från en länk kommer utifrån. Bara bokstäver, siffror,
    // bindestreck och snedstreck, och sökvägen måste hamna under roten.
    private string PathOf(string key)
    {
        if (!IsValidKey(key))
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(_root, key));
        return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException("Invalid storage key.", nameof(key));
    }

    private static bool IsValidKey(string key) => ValidKey().IsMatch(key) && !key.Contains("..");

    private static async Task<string> ReadTypeAsync(string path, CancellationToken ct) =>
        File.Exists(path + ".type")
            ? await File.ReadAllTextAsync(path + ".type", ct)
            : "application/octet-stream";

    [GeneratedRegex("^[A-Za-z0-9_-]+(/[A-Za-z0-9_-]+)*$")]
    private static partial Regex ValidKey();
}
