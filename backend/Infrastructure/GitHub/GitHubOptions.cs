namespace Backend.Infrastructure.GitHub;

/// <summary>
/// GitHub:... i appsettings/user-secrets. PrivateKey och PrivateKeyPath är hemligheter: user-secrets lokalt
/// och miljövariabler på Render, aldrig i appsettings. PrivateKey kan vara PEM eller base64-kodad PEM, vilket
/// gör den enklare att lägga i en miljövariabel.
/// </summary>
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public long AppId { get; set; }

    // Dev-fallback: innan skolan hade en GitHub-anslutning i databasen kom installationen härifrån.
    // Kan tas bort när alla miljöer har kopplat skolan via Inställningar.
    public long InstallationId { get; set; }
    public string Org { get; set; } = "";

    public string AppSlug { get; set; } = "";
    public string PrivateKeyPath { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "https://api.github.com/";
    public string UserAgent { get; set; } = "RebornLMS";

    public bool HasPrivateKey =>
        !string.IsNullOrWhiteSpace(PrivateKeyPath) || !string.IsNullOrWhiteSpace(PrivateKey);

    /// <summary>App-credentials som krävs för att prata med GitHub. Installationen är per skola.</summary>
    public bool IsAppConfigured =>
        AppId > 0 && HasPrivateKey && !string.IsNullOrWhiteSpace(AppSlug);

    // Dev-fallback: används bara när skolan saknar en rad i SchoolGitHubInstallations.
    public bool HasLegacyInstallation =>
        InstallationId > 0 && !string.IsNullOrWhiteSpace(Org);
}
