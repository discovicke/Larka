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

    /// <summary>
    /// Länkar till flera filer i ett anrop, per nyckel. Webbläsaren laddar ner varje fil under dess eget namn
    /// (FileName), inte under nyckeln. Används för listor med bilagor och inlämnade filer.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetDownloadUrlsAsync(
        IReadOnlyCollection<StoredFile> files, TimeSpan expiresIn, CancellationToken ct);

    /// <summary>Kopierar filen till en ny nyckel under folder, så att kopian lever kvar när originalet tas bort.</summary>
    Task<string> CopyAsync(string sourceKey, string folder, CancellationToken ct);

    /// <summary>Tar bort filen. En fil som redan är borta är inget fel.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>En sparad fil: nyckeln i lagringen och namnet den laddas ner som.</summary>
public record StoredFile(string Key, string FileName);
