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
    public long InstallationId { get; set; }
    public string Org { get; set; } = "";
    public string PrivateKeyPath { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "https://api.github.com/";
    public string UserAgent { get; set; } = "RebornLMS";

    public bool HasPrivateKey =>
        !string.IsNullOrWhiteSpace(PrivateKeyPath) || !string.IsNullOrWhiteSpace(PrivateKey);

    public bool IsConfigured =>
        AppId > 0 && InstallationId > 0 && HasPrivateKey && !string.IsNullOrWhiteSpace(Org);
}
