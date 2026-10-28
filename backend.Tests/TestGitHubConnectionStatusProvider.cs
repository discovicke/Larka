using Backend.Application.Abstractions;

namespace backend.Tests;

// Statusen i endpoint-testerna ska vara deterministisk och aldrig gå ut på nätet.
public sealed class TestGitHubConnectionStatusProvider : IGitHubConnectionStatusProvider
{
    public GitHubConnectionStatus Status { get; set; } = new(
        Configured: false,
        AppId: 0,
        InstallationId: 0,
        Org: "skol-org",
        ApiBaseUrl: "https://api.github.com/",
        Connected: false,
        AccountLogin: null,
        LastVerifiedAt: null,
        Error: null,
        MissingSettings: ["AppId", "InstallationId", "PrivateKey"]);

    public Task<GitHubConnectionStatus> GetStatusAsync(CancellationToken ct) =>
        Task.FromResult(Status);
}
