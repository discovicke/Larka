namespace Backend.Infrastructure.Auth;

/// <summary>
/// Auth:Entra i appsettings, satt av infra/entra-setup.sh via GitHub-miljön production. Inget av det är hemligt.
/// Utan båda värdena finns ingen Microsoft-inloggning.
/// </summary>
public sealed class EntraOptions
{
    public const string SectionName = "Auth:Entra";

    public string ClientId { get; set; } = "";
    public string TenantId { get; set; } = "";

    /// <summary>
    /// Object id (oid) för den som körde infra/entra-setup.sh. Första gången hen loggar in med Microsoft kopplas
    /// skolan till tenanten och hen blir admin. Tomt betyder att admin kopplar skolan själv.
    /// </summary>
    public string AdminObjectId { get; set; } = "";

    /// <summary>Organisationens namn i Entra ID. Skolan får namnet när den kopplas första gången.</summary>
    public string SchoolName { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(TenantId);

    public string Authority => $"https://login.microsoftonline.com/{TenantId}/v2.0";
}
