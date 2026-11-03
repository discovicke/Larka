namespace Backend.Infrastructure.Auth;

/// <summary>
/// Första admin för en skola som inte loggar in med Microsoft (Auth:FirstAdmin). Så länge skolan saknar en admin som
/// kan logga in skapar Lärka en vid start med e-posten och lösenordet. Därefter gör inställningen ingenting.
/// </summary>
public sealed class FirstAdminOptions
{
    public const string SectionName = "Auth:FirstAdmin";
    public const int MinPasswordLength = 8;

    public string Email { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>Namnet som visas. Tomt betyder att det byggs ur e-postadressen.</summary>
    public string Name { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Email) || !string.IsNullOrWhiteSpace(Password);
}
