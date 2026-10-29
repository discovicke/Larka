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

    // GitHub-användarnamn, satt via OAuth. Null tills eleven har kopplat sitt GitHub-konto.
    public string? GitHubLogin { get; set; }

    // Notiser av dessa slag vill användaren inte ha i appen.
    public List<NotificationKind> MutedNotificationKinds { get; set; } = [];
}
