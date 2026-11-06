using System.Net;
using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Infrastructure.GitHub;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace backend.Tests;

// Testar den sanerade statusen och verifieringen utan nätverk. GitHub-klienten är utbytt mot en fake.
public class GitHubConnectionServiceTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-10-29T09:00:00Z");
    private static readonly Guid _schoolId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetStatus_WithoutConfiguration_ReturnsMissingSettingsWithoutCallingGitHub()
    {
        var github = new TestGitHubAppService();
        var service = CreateService(github, new GitHubOptions());

        var status = await service.GetStatusAsync(_schoolId, CancellationToken.None);

        Assert.False(status.Configured);
        Assert.False(status.Connected);
        Assert.Contains("AppId", status.MissingSettings);
        Assert.Contains("PrivateKey", status.MissingSettings);
        Assert.Contains("AppSlug", status.MissingSettings);
    }

    [Fact]
    public async Task GetStatus_WithLegacyInstallation_ReturnsConnectedAccount()
    {
        var github = new TestGitHubAppService
        {
            Installation = new GitHubInstallation(
                2, "EduEdugrade-Sandbox", "Organization", 1, TestGitHubAppService.FullPermissions)
        };
        var service = CreateService(github, ConfiguredOptions(legacy: true));

        var status = await service.GetStatusAsync(_schoolId, CancellationToken.None);

        Assert.True(status.Configured);
        Assert.True(status.Connected);
        Assert.Equal("EduEdugrade-Sandbox", status.AccountLogin);
        Assert.Equal(_now, status.LastVerifiedAt);
        Assert.Empty(status.MissingSettings);
        Assert.Equal(2, status.InstallationId);
        Assert.True(status.UsingLegacyInstallation);
    }

    [Fact]
    public async Task GetStatus_WhenSavedInstallationIsDisconnected_DoesNotUseLegacyFallback()
    {
        var github = new TestGitHubAppService();
        var repository = new FakeInstallationRepository();
        repository.Add(new SchoolGitHubInstallation
        {
            SchoolId = _schoolId,
            InstallationId = 2,
            Org = "EduEdugrade-Sandbox",
            Connected = false
        });
        var service = CreateService(github, ConfiguredOptions(legacy: true), repository);

        var status = await service.GetStatusAsync(_schoolId, CancellationToken.None);

        Assert.False(status.Connected);
        Assert.Equal(2, status.InstallationId);
        Assert.Equal(0, github.InstallationCalls);
    }

    [Fact]
    public async Task GetStatus_WhenSavedInstallationIsGone_ClearsItSoAReinstallIsPossible()
    {
        var github = new TestGitHubAppService
        {
            InstallationException = new GitHubApiException(
                HttpStatusCode.NotFound, """{"message":"Not Found"}""")
        };
        var repository = new FakeInstallationRepository();
        repository.Add(new SchoolGitHubInstallation
        {
            SchoolId = _schoolId,
            InstallationId = 178202846,
            Org = "EduEdugrade-Sandbox",
            Connected = true,
            Enabled = true
        });
        var service = CreateService(github, ConfiguredOptions(), repository);

        var status = await service.GetStatusAsync(_schoolId, CancellationToken.None);

        Assert.False(status.Connected);
        Assert.False(status.Enabled);
        Assert.Equal(0, status.InstallationId);
        Assert.Contains("finns inte längre", status.Error);
        var saved = await repository.GetForSchoolAsync(_schoolId, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(0, saved.InstallationId);
        Assert.False(saved.Connected);
        Assert.False(saved.Enabled);
        Assert.Equal(_now, saved.DisconnectedAt);
    }

    [Fact]
    public async Task ReconnectAsync_WhenSavedInstallationIsGone_ClearsItSoAReinstallIsPossible()
    {
        var github = new TestGitHubAppService
        {
            InstallationException = new GitHubApiException(
                HttpStatusCode.NotFound, """{"message":"Not Found"}""")
        };
        var repository = new FakeInstallationRepository();
        repository.Add(new SchoolGitHubInstallation
        {
            SchoolId = _schoolId,
            InstallationId = 178202846,
            Org = "EduEdugrade-Sandbox",
            Connected = false
        });
        var service = CreateService(github, ConfiguredOptions(), repository);

        var status = await service.ReconnectAsync(_schoolId, CancellationToken.None);

        Assert.False(status.Connected);
        Assert.Equal(0, status.InstallationId);
        Assert.Contains("finns inte längre", status.Error);
        var saved = await repository.GetForSchoolAsync(_schoolId, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(0, saved.InstallationId);
    }

    [Fact]
    public async Task ConnectAsync_VerifiesAndSavesInstallation()
    {
        var github = new TestGitHubAppService
        {
            Installation = new GitHubInstallation(
                5678, "skol-org", "Organization", 1, TestGitHubAppService.FullPermissions)
        };
        var repository = new FakeInstallationRepository();
        var service = CreateService(github, ConfiguredOptions(), repository);

        var status = await service.ConnectAsync(_schoolId, 5678, CancellationToken.None);

        Assert.True(status.Connected);
        Assert.Equal("skol-org", status.Org);
        Assert.Equal(_now, status.LastVerifiedAt);
        Assert.False(status.UsingLegacyInstallation);
        var saved = await repository.GetForSchoolAsync(_schoolId, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.True(saved.Connected);
        Assert.Equal(5678, saved.InstallationId);
        Assert.Equal("skol-org", saved.Org);
        Assert.Equal(_now, saved.LastVerifiedAt);
    }

    [Fact]
    public async Task ConnectAsync_WhenPermissionsAreMissing_ReturnsErrorAndDoesNotSave()
    {
        var permissions = new Dictionary<string, string>(TestGitHubAppService.FullPermissions)
        {
            ["contents"] = "read"
        };
        var github = new TestGitHubAppService
        {
            Installation = new GitHubInstallation(5678, "skol-org", "Organization", 1, permissions)
        };
        var repository = new FakeInstallationRepository();
        var service = CreateService(github, ConfiguredOptions(), repository);

        var status = await service.ConnectAsync(_schoolId, 5678, CancellationToken.None);

        Assert.False(status.Connected);
        Assert.Contains("contents (write)", status.Error);
        Assert.Null(await repository.GetForSchoolAsync(_schoolId, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectAsync_WhenAccountIsNotAnOrganization_ReturnsError()
    {
        var github = new TestGitHubAppService
        {
            Installation = new GitHubInstallation(
                5678, "some-user", "User", 1, TestGitHubAppService.FullPermissions)
        };
        var service = CreateService(github, ConfiguredOptions(), new FakeInstallationRepository());

        var status = await service.ConnectAsync(_schoolId, 5678, CancellationToken.None);

        Assert.False(status.Connected);
        Assert.Contains("organisation", status.Error);
    }

    [Fact]
    public async Task ConnectAsync_WhenInstallationIsGone_ReturnsClearError()
    {
        var github = new TestGitHubAppService
        {
            InstallationException = new GitHubApiException(
                HttpStatusCode.NotFound, """{"message":"Not Found"}""")
        };
        var service = CreateService(github, ConfiguredOptions(), new FakeInstallationRepository());

        var status = await service.ConnectAsync(_schoolId, 5678, CancellationToken.None);

        Assert.False(status.Connected);
        Assert.Contains("finns inte längre", status.Error);
    }

    [Fact]
    public async Task ConnectAsync_WhenInstallationBelongsToAnotherSchool_ReturnsConflict()
    {
        var github = new TestGitHubAppService
        {
            Installation = new GitHubInstallation(
                5678, "skol-org", "Organization", 1, TestGitHubAppService.FullPermissions)
        };
        var repository = new FakeInstallationRepository();
        repository.Add(new SchoolGitHubInstallation
        {
            SchoolId = Guid.NewGuid(),
            InstallationId = 5678,
            Org = "skol-org",
            Connected = true
        });
        var service = CreateService(github, ConfiguredOptions(), repository);

        var status = await service.ConnectAsync(_schoolId, 5678, CancellationToken.None);

        Assert.False(status.Connected);
        Assert.Contains("annan skola", status.Error);
    }

    [Fact]
    public async Task DisconnectAsync_WithLegacyInstallation_CreatesDisconnectedRow()
    {
        var repository = new FakeInstallationRepository();
        var service = CreateService(
            new TestGitHubAppService(), ConfiguredOptions(legacy: true), repository);

        await service.DisconnectAsync(_schoolId, CancellationToken.None);

        var saved = await repository.GetForSchoolAsync(_schoolId, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.False(saved.Connected);
        Assert.Equal(2, saved.InstallationId);
        Assert.Equal(_now, saved.DisconnectedAt);
    }

    private static GitHubConnectionService CreateService(
        TestGitHubAppService github,
        GitHubOptions options,
        FakeInstallationRepository? repository = null) =>
        new(
            github,
            repository ?? new FakeInstallationRepository(),
            new FakeUnitOfWork(),
            Options.Create(options),
            new TestTimeProvider(_now),
            NullLogger<GitHubConnectionService>.Instance);

    private static GitHubOptions ConfiguredOptions(bool legacy = false) => new()
    {
        AppId = 1,
        AppSlug = "test-app",
        InstallationId = legacy
            ? 2
            : 0,
        Org = legacy
            ? "EduEdugrade-Sandbox"
            : "",
        PrivateKey = "test-key",
        ApiBaseUrl = "https://api.github.com/"
    };

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeInstallationRepository : ISchoolGitHubInstallationRepository
    {
        private readonly List<SchoolGitHubInstallation> _items = [];

        public Task<SchoolGitHubInstallation?> GetForSchoolAsync(
            Guid schoolId, CancellationToken ct) =>
            Task.FromResult(_items.FirstOrDefault(item => item.SchoolId == schoolId));

        public Task<SchoolGitHubInstallation?> GetByInstallationAsync(
            long installationId, CancellationToken ct) =>
            Task.FromResult(_items.FirstOrDefault(item => item.InstallationId == installationId));

        public void Add(SchoolGitHubInstallation installation) => _items.Add(installation);
    }
}
