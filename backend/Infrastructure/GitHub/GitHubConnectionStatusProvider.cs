using Backend.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.GitHub;

/// <summary>
/// Läser GitHub-konfigurationen och verifierar installationen mot GitHub. Svaret är sanerat och
/// innehåller aldrig private key, client secret, webhook secret eller installation token.
/// </summary>
public sealed class GitHubConnectionStatusProvider(
    IGitHubAppService github,
    IOptions<GitHubOptions> options,
    TimeProvider time,
    ILogger<GitHubConnectionStatusProvider> logger) : IGitHubConnectionStatusProvider
{
    private readonly GitHubOptions _options = options.Value;

    public async Task<GitHubConnectionStatus> GetStatusAsync(CancellationToken ct)
    {
        var missing = MissingSettings();
        if (missing.Count > 0)
        {
            return Status(false, null, null, missing);
        }

        try
        {
            var installation = await github.GetInstallationAsync(_options.InstallationId, ct);
            if (!string.Equals(installation.AccountLogin, _options.Org, StringComparison.OrdinalIgnoreCase))
            {
                return Status(
                    false,
                    installation.AccountLogin,
                    time.GetUtcNow(),
                    [],
                    $"Installationen tillhör {installation.AccountLogin}, inte {_options.Org}.");
            }

            return Status(true, installation.AccountLogin, time.GetUtcNow(), []);
        }
        catch (GitHubApiException exception)
        {
            // GitHubs eget felmeddelande är säkert att visa och hjälper admin att se t.ex. 401/404.
            return Status(false, null, time.GetUtcNow(), [], exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Undvik att läcka sökvägar eller annan intern konfiguration till klienten.
            logger.LogWarning(exception, "Could not verify the GitHub installation");
            return Status(
                false,
                null,
                time.GetUtcNow(),
                [],
                "Kunde inte verifiera GitHub-anslutningen. Kontrollera serverns konfiguration.");
        }
    }

    private GitHubConnectionStatus Status(
        bool connected,
        string? accountLogin,
        DateTimeOffset? lastVerifiedAt,
        IReadOnlyList<string> missingSettings,
        string? error = null) =>
        new(
            Configured: missingSettings.Count == 0,
            AppId: _options.AppId,
            InstallationId: _options.InstallationId,
            Org: _options.Org,
            ApiBaseUrl: _options.ApiBaseUrl,
            Connected: connected,
            AccountLogin: accountLogin,
            LastVerifiedAt: lastVerifiedAt,
            Error: error,
            MissingSettings: missingSettings);

    private List<string> MissingSettings()
    {
        var missing = new List<string>();
        if (_options.AppId <= 0)
        {
            missing.Add("AppId");
        }

        if (_options.InstallationId <= 0)
        {
            missing.Add("InstallationId");
        }

        if (string.IsNullOrWhiteSpace(_options.Org))
        {
            missing.Add("Org");
        }

        if (!_options.HasPrivateKey)
        {
            missing.Add("PrivateKey");
        }

        return missing;
    }
}
