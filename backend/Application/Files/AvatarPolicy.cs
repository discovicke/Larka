using Backend.Application.Dtos;

namespace Backend.Application.Files;

// Profilbilder: bara bildtyper och högst 5 MB. Typen sätts från filändelsen, som för andra filer (FileUploadPolicy).
public static class AvatarPolicy
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> _extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp",
    };

    /// <summary>Felmeddelandet (på engelska, som övriga fel i API:et), eller null om bilden är godkänd.</summary>
    public static string? Validate(FileUpload file)
    {
        if (!_extensions.Contains(Path.GetExtension(file.FileName)))
        {
            return "a profile picture must be a png, jpg, gif or webp image";
        }

        if (file.SizeBytes <= 0)
        {
            return "the image is empty";
        }

        return file.SizeBytes > MaxFileBytes
            ? $"a profile picture can be at most {MaxFileBytes / 1024 / 1024} MB"
            : null;
    }

    /// <summary>
    /// Adressen klienten visar bilden från, eller null utan bild. Den sista delen är bildens egen guid, så adressen
    /// byts när bilden byts och kan cachas. Se AvatarService.GetSignedUrlAsync.
    /// </summary>
    public static string? UrlFor(Guid userId, string? avatarKey) =>
        avatarKey is null
            ? null
            : $"/api/avatars/{userId}/{Path.GetFileName(avatarKey)}";
}
