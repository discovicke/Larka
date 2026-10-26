namespace Backend.Application.Abstractions;

// Filerna ligger i en privat bucket. Resten av backend känner bara till nycklar och tidsbegränsade länkar,
// inte vilken lagringstjänst som ligger bakom (se Infrastructure/Storage).
public interface IFileStorage
{
    /// <summary>Hur länge en länk från GetSignedUrlAsync gäller.</summary>
    static readonly TimeSpan SignedUrlLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Sparar filen under folder (t.ex. "content/{contentId}") och returnerar nyckeln den fick.</summary>
    Task<string> UploadAsync(string folder, Stream content, string contentType, CancellationToken ct);

    /// <summary>En länk som går att läsa filen från tills den löper ut. Skapas först när filen läses.</summary>
    Task<string> GetSignedUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}
