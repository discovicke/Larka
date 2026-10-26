using System.Collections.Concurrent;
using Backend.Application.Abstractions;

namespace Backend.Infrastructure.Storage;

// För tester och lokal körning utan Supabase-projekt. Filerna försvinner när appen stängs av.
public class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public IReadOnlyCollection<string> Keys => _files.Keys.ToList();

    public async Task<string> UploadAsync(string folder, Stream content, string contentType, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        var key = $"{folder}/{Guid.NewGuid()}";
        _files[key] = buffer.ToArray();
        return key;
    }

    public Task<string> GetSignedUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct) =>
        _files.ContainsKey(key)
            ? Task.FromResult($"https://storage.local/{key}?expires={(int)expiresIn.TotalSeconds}")
            : throw new FileNotFoundException("No such file in storage.", key);

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        _files.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
