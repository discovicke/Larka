using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; }

    // Hash från ASP.NET Cores PasswordHasher, för den enkla inloggningen. Null betyder att användaren inte kan logga in med lösenord.
    public string? PasswordHash { get; set; }

    // Användarens id i Microsoft Entra ID (oid), satt vid första Microsoft-inloggningen. Användaren känns igen på
    // det, inte på e-posten, eftersom e-postadresser kan bytas.
    public string? ExternalId { get; set; }

    // När admin spärrade användaren. En spärrad användare kan inte logga in, men allt hen gjort finns kvar.
    public DateTimeOffset? DeactivatedAt { get; set; }

    // GitHub-användarnamn, satt via OAuth. Null tills eleven har kopplat sitt GitHub-konto.
    public string? GitHubLogin { get; set; }

    // Nyckeln till profilbilden i fillagringen (avatars/{userId}/{guid}), eller null när användaren inte har någon.
    // En ny bild får alltid en ny nyckel, så att länken till den gamla slutar fungera.
    public string? AvatarKey { get; set; }

    // Notiser av dessa slag vill användaren inte ha i appen.
    public List<NotificationKind> MutedNotificationKinds { get; set; } = [];
}
