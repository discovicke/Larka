using Backend.Application.Abstractions;

namespace backend.Tests;

// Statusen i endpoint-testerna ska vara deterministisk och aldrig gå ut på nätet.
public sealed class TestGitHubConnectionStatusProvider : IGitHubConnectionStatusProvider
{
    public Task<GitHubConnectionStatus> GetStatusAsync(CancellationToken ct) =>
        Task.FromResult(new GitHubConnectionStatus(
            Configured: false,
            AppId: 0,
            InstallationId: 0,
            Org: "skol-org",
            ApiBaseUrl: "https://api.github.com/",
            Connected: false,
            AccountLogin: null,
            LastVerifiedAt: null,
            Error: null,
            MissingSettings: ["AppId", "InstallationId", "PrivateKey"]));
}
