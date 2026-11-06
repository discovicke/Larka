using System.Net;
using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.GitHub;

/// <summary>
/// Läser GitHub-konfigurationen och verifierar skolans installation mot GitHub. App-credentials är
/// gemensamma, medan installation och organisation sparas per skola. Svaret är sanerat och innehåller
/// aldrig private key, client secret, webhook secret eller installation token.
/// </summary>
public sealed class GitHubConnectionService(
    IGitHubAppService github,
    ISchoolGitHubInstallationRepository installations,
    IUnitOfWork unitOfWork,
    IOptions<GitHubOptions> options,
    TimeProvider time,
    ILogger<GitHubConnectionService> logger) : IGitHubConnectionService
{
    private static readonly (string Name, string Level)[] _requiredPermissions =
    [
        ("contents", "write"),
        ("administration", "write"),
        ("checks", "read"),
        ("actions", "read"),
        ("metadata", "read")
    ];

    private static readonly Dictionary<string, int> _permissionLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["read"] = 1,
        ["write"] = 2,
        ["admin"] = 3
    };

    private readonly GitHubOptions _options = options.Value;

    public bool IsConfigured => _options.IsAppConfigured;

    public IReadOnlyList<string> MissingSettings
    {
        get
        {
            var missing = new List<string>();
            if (_options.AppId <= 0)
            {
                missing.Add("AppId");
            }

            if (!_options.HasPrivateKey)
            {
                missing.Add("PrivateKey");
            }

            if (string.IsNullOrWhiteSpace(_options.AppSlug))
            {
                missing.Add("AppSlug");
            }

            return missing;
        }
    }

    public string BuildInstallUrl(string state)
    {
        if (!_options.IsAppConfigured || string.IsNullOrWhiteSpace(_options.AppSlug))
        {
            throw new InvalidOperationException(
                "GitHub:AppId, GitHub:PrivateKey och GitHub:AppSlug måste vara konfigurerade.");
        }

        return "https://github.com/apps/"
            + Uri.EscapeDataString(_options.AppSlug)
            + "/installations/new?state="
            + Uri.EscapeDataString(state);
    }

    public async Task<GitHubConnectionStatus> GetStatusAsync(Guid schoolId, CancellationToken ct)
    {
        var missing = MissingSettings;
        var saved = await installations.GetForSchoolAsync(schoolId, ct);

        if (!IsConfigured)
        {
            return Status(
                saved?.InstallationId ?? 0,
                saved?.Org ?? "",
                false,
                null,
                saved?.LastVerifiedAt,
                null,
                missing,
                enabled: saved?.Enabled ?? false);
        }

        // En lokal frånkoppling ska vinna över dev-fallbacken. Annars skulle disconnect inte stoppa
        // provisionering när GitHub:InstallationId finns kvar i miljön.
        if (saved is not null && !saved.Connected)
        {
            return Status(
                saved.InstallationId,
                saved.Org,
                false,
                null,
                saved.LastVerifiedAt,
                null,
                missing,
                enabled: saved.Enabled);
        }

        if (saved is not null)
        {
            var (installation, error, installationMissing) = await VerifyAsync(
                saved.InstallationId, saved.Org, ct);
            if (error is null && installation is not null)
            {
                saved.Org = installation.AccountLogin;
                saved.LastVerifiedAt = time.GetUtcNow();
                saved.Connected = true;
                await unitOfWork.SaveChangesAsync(ct);
            }
            else if (installationMissing)
            {
                // Appen är avinstallerad på GitHub. Nollställ den döda installationen så att admin
                // kan starta en ny installation i stället för att återansluta till ett ID som inte finns.
                ClearInstallation(saved);
                await unitOfWork.SaveChangesAsync(ct);
            }

            return Status(
                saved.InstallationId,
                saved.Org,
                error is null,
                installation?.AccountLogin,
                saved.LastVerifiedAt,
                error,
                missing,
                enabled: saved.Enabled);
        }

        // Dev-fallback tills skolan har kopplat via Inställningar. Kan tas bort senare.
        if (_options.HasLegacyInstallation)
        {
            var (installation, error, _) = await VerifyAsync(
                _options.InstallationId, _options.Org, ct);
            return Status(
                _options.InstallationId,
                _options.Org,
                error is null,
                installation?.AccountLogin,
                error is null
                    ? time.GetUtcNow()
                    : null,
                error,
                missing,
                usingLegacyInstallation: true,
                enabled: true);
        }

        return Status(0, "", false, null, null, null, missing);
    }

    public async Task<GitHubConnectionStatus> ConnectAsync(
        Guid schoolId, long installationId, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return Status(
                installationId,
                "",
                false,
                null,
                null,
                "GitHub är inte konfigurerat på servern.",
                MissingSettings);
        }

        if (installationId <= 0)
        {
            return Status(0, "", false, null, null, "Installations-ID saknas.", MissingSettings);
        }

        var (installation, error, installationMissing) = await VerifyAsync(installationId, "", ct);
        if (error is not null || installation is null)
        {
            if (installationMissing)
            {
                // Även en återanslutning kan träffa en installation som tagits bort på GitHub.
                // Rensa då den sparade raden så att klienten kan erbjuda en ny installation.
                await ClearMissingInstallationAsync(schoolId, installationId, ct);
            }

            var statusInstallationId = installationMissing
                ? 0
                : installationId;
            return Status(
                statusInstallationId,
                "",
                false,
                installation?.AccountLogin,
                null,
                error,
                MissingSettings);
        }

        var usedBy = await installations.GetByInstallationAsync(installationId, ct);
        if (usedBy is not null && usedBy.SchoolId != schoolId)
        {
            return Status(
                installationId,
                installation.AccountLogin,
                false,
                installation.AccountLogin,
                null,
                "Installationen är redan kopplad till en annan skola.",
                MissingSettings);
        }

        var saved = await installations.GetForSchoolAsync(schoolId, ct);
        var now = time.GetUtcNow();
        if (saved is null)
        {
            saved = new SchoolGitHubInstallation
            {
                SchoolId = schoolId,
                InstallationId = installationId,
                Org = installation.AccountLogin,
                Connected = true,
                Enabled = true,
                ConnectedAt = now,
                DisconnectedAt = null,
                LastVerifiedAt = now
            };
            installations.Add(saved);
        }
        else
        {
            saved.InstallationId = installationId;
            saved.Org = installation.AccountLogin;
            saved.Connected = true;
            saved.Enabled = true;
            saved.ConnectedAt = now;
            saved.DisconnectedAt = null;
            saved.LastVerifiedAt = now;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Status(
            installationId,
            installation.AccountLogin,
            true,
            installation.AccountLogin,
            now,
            null,
            MissingSettings,
            enabled: true);
    }

    public async Task<GitHubConnectionStatus> ReconnectAsync(
        Guid schoolId, CancellationToken ct)
    {
        var saved = await installations.GetForSchoolAsync(schoolId, ct);
        if (saved is null || saved.InstallationId <= 0)
        {
            return Status(
                0,
                "",
                false,
                null,
                null,
                "Ingen sparad GitHub-installation finns. Anslut GitHub först.",
                MissingSettings);
        }

        return await ConnectAsync(schoolId, saved.InstallationId, ct);
    }

    public async Task<GitHubConnectionStatus> SetEnabledAsync(
        Guid schoolId, bool enabled, CancellationToken ct)
    {
        var saved = await installations.GetForSchoolAsync(schoolId, ct);
        if (saved is null || !saved.Connected || saved.InstallationId <= 0)
        {
            return Status(
                saved?.InstallationId ?? 0,
                saved?.Org ?? "",
                false,
                null,
                saved?.LastVerifiedAt,
                "Anslut GitHub innan du aktiverar det.",
                MissingSettings,
                enabled: false);
        }

        saved.Enabled = enabled;
        await unitOfWork.SaveChangesAsync(ct);
        return Status(
            saved.InstallationId,
            saved.Org,
            true,
            saved.Org,
            saved.LastVerifiedAt,
            null,
            MissingSettings,
            enabled: enabled);
    }

    public async Task DisconnectAsync(Guid schoolId, CancellationToken ct)
    {
        var saved = await installations.GetForSchoolAsync(schoolId, ct);
        var now = time.GetUtcNow();
        if (saved is null)
        {
            // Utan en sparad rad finns inget att koppla från. Dev-fallbacken måste ändå markeras
            // frånkopplad, annars skulle provisioneringen fortsätta använda miljövariablerna.
            if (!_options.HasLegacyInstallation)
            {
                return;
            }

            saved = new SchoolGitHubInstallation
            {
                SchoolId = schoolId,
                InstallationId = _options.InstallationId,
                Org = _options.Org,
                Connected = false,
                Enabled = false,
                ConnectedAt = now,
                DisconnectedAt = now
            };
            installations.Add(saved);
        }
        else
        {
            saved.Connected = false;
            saved.Enabled = false;
            saved.DisconnectedAt = now;
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ClearMissingInstallationAsync(
        Guid schoolId, long installationId, CancellationToken ct)
    {
        var saved = await installations.GetForSchoolAsync(schoolId, ct);
        if (saved is null || saved.InstallationId != installationId)
        {
            return;
        }

        ClearInstallation(saved);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private void ClearInstallation(SchoolGitHubInstallation saved)
    {
        saved.InstallationId = 0;
        saved.Connected = false;
        saved.Enabled = false;
        saved.DisconnectedAt = time.GetUtcNow();
    }

    private async Task<(GitHubInstallation? Installation, string? Error, bool NotFound)> VerifyAsync(
        long installationId, string expectedOrg, CancellationToken ct)
    {
        try
        {
            var installation = await github.GetInstallationAsync(installationId, ct)
                ?? throw new InvalidOperationException("GitHub returned no installation.");

            if (!string.Equals(
                installation.AccountType, "Organization", StringComparison.OrdinalIgnoreCase))
            {
                return (installation, "GitHub-appen måste vara installerad på en organisation.", false);
            }

            if (!string.IsNullOrWhiteSpace(expectedOrg)
                && !string.Equals(
                    installation.AccountLogin, expectedOrg, StringComparison.OrdinalIgnoreCase))
            {
                return (
                    installation,
                    $"Installationen tillhör {installation.AccountLogin}, inte {expectedOrg}.",
                    false);
            }

            var missingPermissions = RequiredPermissionsMissing(installation.Permissions);
            if (missingPermissions.Count > 0)
            {
                return (
                    installation,
                    "Installationen saknar behörigheter: " + string.Join(", ", missingPermissions) + ".",
                    false);
            }

            return (installation, null, false);
        }
        catch (GitHubApiException exception)
        {
            var notFound = exception.StatusCode == HttpStatusCode.NotFound;
            // GitHubs eget felmeddelande är säkert att visa och hjälper admin att se t.ex. 401/404.
            var message = notFound
                ? "Installationen finns inte längre på GitHub. Installera appen igen."
                : exception.Message;
            return (null, message, notFound);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Undvik att läcka sökvägar eller annan intern konfiguration till klienten.
            logger.LogWarning(exception, "Could not verify the GitHub installation");
            return (null, "Kunde inte verifiera GitHub-anslutningen. Kontrollera serverns konfiguration.", false);
        }
    }

    private static List<string> RequiredPermissionsMissing(
        IReadOnlyDictionary<string, string> permissions)
    {
        var missing = new List<string>();
        foreach (var (name, requiredLevel) in _requiredPermissions)
        {
            var actual = permissions
                .FirstOrDefault(permission =>
                    string.Equals(permission.Key, name, StringComparison.OrdinalIgnoreCase))
                .Value;
            if (string.IsNullOrWhiteSpace(actual)
                || !_permissionLevels.TryGetValue(actual, out var actualRank)
                || !_permissionLevels.TryGetValue(requiredLevel, out var requiredRank)
                || actualRank < requiredRank)
            {
                missing.Add($"{name} ({requiredLevel})");
            }
        }

        return missing;
    }

    private GitHubConnectionStatus Status(
        long installationId,
        string org,
        bool connected,
        string? accountLogin,
        DateTimeOffset? lastVerifiedAt,
        string? error,
        IReadOnlyList<string>? missingSettings = null,
        bool usingLegacyInstallation = false,
        bool enabled = false) =>
        new(
            Configured: IsConfigured,
            AppId: _options.AppId,
            InstallationId: installationId,
            Org: org,
            ApiBaseUrl: _options.ApiBaseUrl,
            Connected: connected,
            AccountLogin: accountLogin,
            LastVerifiedAt: lastVerifiedAt,
            Error: error,
            MissingSettings: missingSettings ?? MissingSettings,
            UsingLegacyInstallation: usingLegacyInstallation,
            Enabled: enabled);
}
