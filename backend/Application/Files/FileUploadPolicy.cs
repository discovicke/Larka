using Backend.Application.Dtos;

namespace Backend.Application.Files;

// Gränserna för uppladdade filer. Filtypen avgörs av filändelsen och lagras med en typ vi själva väljer,
// så att det klienten skickar aldrig blir en Content-Type (t.ex. text/html som kan köras i webbläsaren).
public static class FileUploadPolicy
{
    public const long MaxFileBytes = 20 * 1024 * 1024;

    // Tak för alla filer i en och samma förfrågan.
    public const long MaxRequestBytes = 50 * 1024 * 1024;

    public const int MaxFilesPerRequest = 10;

    private static readonly Dictionary<string, string> _contentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".zip"] = "application/zip",
    };

    /// <summary>Typen som filen lagras med, eller null om filtypen inte är tillåten.</summary>
    public static string? ContentTypeFor(string fileName) =>
        _contentTypes.TryGetValue(Path.GetExtension(fileName), out var contentType)
            ? contentType
            : null;

    /// <summary>Filnamnet utan sökväg, så att en klient inte kan styra vilken nyckel eller mapp som används.</summary>
    public static string CleanFileName(string fileName) => Path.GetFileName(fileName).Trim();

    /// <summary>Felmeddelandet (på engelska, som övriga fel i API:et), eller null om uppladdningen är godkänd.</summary>
    public static string? Validate(IReadOnlyCollection<FileUpload> files)
    {
        if (files.Count > MaxFilesPerRequest)
        {
            return $"at most {MaxFilesPerRequest} files can be uploaded at once";
        }

        if (files.Sum(file => file.SizeBytes) > MaxRequestBytes)
        {
            return $"the files together can be at most {MaxRequestBytes / 1024 / 1024} MB";
        }

        foreach (var file in files)
        {
            if (string.IsNullOrEmpty(CleanFileName(file.FileName)))
            {
                return "every file needs a name";
            }

            if (ContentTypeFor(file.FileName) is null)
            {
                return $"{CleanFileName(file.FileName)} has a file type that is not allowed";
            }

            if (file.SizeBytes <= 0)
            {
                return $"{CleanFileName(file.FileName)} is empty";
            }

            if (file.SizeBytes > MaxFileBytes)
            {
                return $"{CleanFileName(file.FileName)} is larger than {MaxFileBytes / 1024 / 1024} MB";
            }
        }

        return null;
    }
}
