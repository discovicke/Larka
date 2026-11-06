using System.Net;
using System.Net.Http.Json;
using Backend.Application.Abstractions;
using Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class IntegrationEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record IdResponse(Guid Id);

    private record GitHubStatusResponse(
        bool Configured,
        long AppId,
        long InstallationId,
        string Org,
        string ApiBaseUrl,
        bool Connected,
        string? AccountLogin,
        DateTimeOffset? LastVerifiedAt,
        string? Error,
        List<string> MissingSettings,
        bool UsingLegacyInstallation = false,
        bool Enabled = false,
        int StudentCount = 0,
        int LinkedStudentCount = 0,
        int RepoCount = 0);

    private record InstallLinkResponse(string Url, string State);

    private record TemplateRepoResponse(
        string FullName,
        string HtmlUrl,
        string DefaultBranch,
        bool IsPrivate,
        bool IsArchived);

    private record TemplateCommitResponse(string Sha, string Message, DateTimeOffset? AuthoredAt);

    [Fact]
    public async Task GetGitHubStatus_AsAdmin_ReturnsSanitizedUnconnectedStatus()
    {
        await ResetAsync();

        var status = await _client.GetFromJsonAsync<GitHubStatusResponse>("/api/integrations/github");

        Assert.NotNull(status);
        Assert.True(status.Configured);
        Assert.False(status.Connected);
        Assert.Equal(0, status.InstallationId);
        Assert.Empty(status.MissingSettings);
    }

    [Fact]
    public async Task GetGitHubStatus_AsStudent_ReturnsForbidden()
    {
        var school = await _client.PostAsJsonAsync("/api/schools", new { name = "Integration School" });
        var schoolId = (await school.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        var user = await _client.PostAsJsonAsync("/api/users", new
        {
            schoolId,
            name = "Bo Elev",
            email = $"{Guid.NewGuid():N}@example.com",
            role = "Student"
        });
        var userId = (await user.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/integrations/github");
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Du har inte behörighet", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InstallLink_AsAdmin_ReturnsGitHubInstallationUrlWithState()
    {
        await ResetAsync();

        var link = await _client.GetFromJsonAsync<InstallLinkResponse>("/api/integrations/github/install");

        Assert.NotNull(link);
        Assert.Contains("github.com/apps/test-app/installations/new", link.Url);
        Assert.Contains("state=", link.Url);
        Assert.False(string.IsNullOrWhiteSpace(link.State));
    }

    [Fact]
    public async Task ConnectAndDisconnect_AsAdmin_UpdatesConnectionState()
    {
        await ResetAsync();
        var link = await GetInstallLinkAsync();

        var connectResponse = await _client.PostAsJsonAsync("/api/integrations/github/connect", new
        {
            installationId = 5678,
            state = link.State
        });
        connectResponse.EnsureSuccessStatusCode();
        var connected = await connectResponse.Content.ReadFromJsonAsync<GitHubStatusResponse>();
        Assert.NotNull(connected);
        Assert.True(connected.Connected);
        Assert.Equal("skol-org", connected.Org);
        Assert.Equal(5678, connected.InstallationId);
        Assert.NotNull(connected.LastVerifiedAt);

        var disconnectResponse = await _client.DeleteAsync("/api/integrations/github");
        Assert.Equal(HttpStatusCode.NoContent, disconnectResponse.StatusCode);

        var statusAfterDisconnect = await _client.GetFromJsonAsync<GitHubStatusResponse>(
            "/api/integrations/github");
        Assert.NotNull(statusAfterDisconnect);
        Assert.False(statusAfterDisconnect.Connected);
        Assert.Equal(5678, statusAfterDisconnect.InstallationId);
    }

    [Fact]
    public async Task Templates_ReturnOnlySanitizedTemplateMetadata()
    {
        await ResetAsync();
        var link = await GetInstallLinkAsync();
        await _client.PostAsJsonAsync("/api/integrations/github/connect", new
        {
            installationId = 5678,
            state = link.State
        });

        var github = Assert.IsType<TestGitHubAppService>(
            factory.Services.GetRequiredService<IGitHubAppService>());
        github.TemplateRepositories =
        [
            new GitHubRepository(
                "skol-org/template",
                "https://github.com/skol-org/template",
                "main",
                true,
                true,
                false)
        ];
        github.LatestTemplateCommit = new GitHubCommit(
            "abc123", "Template update", DateTimeOffset.Parse("2026-10-28T09:30:00Z"));

        var templates = await _client.GetFromJsonAsync<List<TemplateRepoResponse>>(
            "/api/integrations/github/templates");
        var template = Assert.Single(templates!);
        Assert.Equal("skol-org/template", template.FullName);
        Assert.True(template.IsPrivate);

        var commit = await _client.GetFromJsonAsync<TemplateCommitResponse>(
            "/api/integrations/github/templates/skol-org/template/latest-commit");
        Assert.Equal("abc123", commit!.Sha);
        Assert.Equal("Template update", commit.Message);

        github.WorkflowNames = ["CI", "Release"];
        var tests = await _client.GetFromJsonAsync<List<string>>(
            "/api/integrations/github/templates/skol-org/template/tests");
        Assert.Equal(["CI", "Release"], tests);
    }

    [Fact]
    public async Task SetEnabled_RequiresConnectionAndTogglesWithoutDisconnecting()
    {
        await ResetAsync();

        var beforeConnect = await _client.PutAsJsonAsync(
            "/api/integrations/github/enabled", new { enabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, beforeConnect.StatusCode);

        var link = await GetInstallLinkAsync();
        var connectResponse = await _client.PostAsJsonAsync("/api/integrations/github/connect", new
        {
            installationId = 5678,
            state = link.State
        });
        connectResponse.EnsureSuccessStatusCode();

        var disabled = await _client.PutAsJsonAsync(
            "/api/integrations/github/enabled", new { enabled = false });
        disabled.EnsureSuccessStatusCode();
        var disabledStatus = await disabled.Content.ReadFromJsonAsync<GitHubStatusResponse>();
        Assert.True(disabledStatus!.Connected);
        Assert.False(disabledStatus.Enabled);

        var enabled = await _client.PutAsJsonAsync(
            "/api/integrations/github/enabled", new { enabled = true });
        enabled.EnsureSuccessStatusCode();
        var enabledStatus = await enabled.Content.ReadFromJsonAsync<GitHubStatusResponse>();
        Assert.True(enabledStatus!.Connected);
        Assert.True(enabledStatus.Enabled);
    }

    [Fact]
    public async Task SetupCallback_WithStateCookie_ConnectsInstallation()
    {
        await ResetAsync();
        var link = await GetInstallLinkAsync();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/integrations/github/setup?installation_id=5678");
        request.Headers.Add("Cookie", $"github_install_state={link.State}");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/settings?github=connected", response.Headers.Location!.ToString());
        var status = await _client.GetFromJsonAsync<GitHubStatusResponse>("/api/integrations/github");
        Assert.NotNull(status);
        Assert.True(status.Connected);
        Assert.Equal(5678, status.InstallationId);
    }

    [Fact]
    public async Task Connect_WithoutValidState_ReturnsBadRequest()
    {
        await ResetAsync();

        var response = await _client.PostAsJsonAsync("/api/integrations/github/connect", new
        {
            installationId = 5678,
            state = "not-a-valid-state"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("kunde inte verifieras", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reconnect_VerifiesSavedInstallation()
    {
        await ResetAsync();
        var link = await GetInstallLinkAsync();
        await _client.PostAsJsonAsync("/api/integrations/github/connect", new
        {
            installationId = 5678,
            state = link.State
        });
        await _client.DeleteAsync("/api/integrations/github");

        var response = await _client.PostAsJsonAsync("/api/integrations/github/connect", new
        {
            installationId = (long?)null,
            state = (string?)null
        });

        response.EnsureSuccessStatusCode();
        var status = await response.Content.ReadFromJsonAsync<GitHubStatusResponse>();
        Assert.NotNull(status);
        Assert.True(status.Connected);
    }

    [Fact]
    public async Task Connect_WhenPermissionsAreMissing_ReturnsBadRequest()
    {
        await ResetAsync();
        var github = factory.Services.GetRequiredService<IGitHubAppService>() as TestGitHubAppService;
        Assert.NotNull(github);
        github.Installation = new GitHubInstallation(
            5678,
            "skol-org",
            "Organization",
            1234,
            new Dictionary<string, string>(TestGitHubAppService.FullPermissions)
            {
                ["contents"] = "read"
            });
        var link = await GetInstallLinkAsync();

        var response = await _client.PostAsJsonAsync("/api/integrations/github/connect", new
        {
            installationId = 5678,
            state = link.State
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("contents (write)", await response.Content.ReadAsStringAsync());
        var status = await _client.GetFromJsonAsync<GitHubStatusResponse>("/api/integrations/github");
        Assert.False(status!.Connected);
    }

    [Fact]
    public async Task Connect_WhenInstallationIsGone_ReturnsClearError()
    {
        await ResetAsync();
        var github = factory.Services.GetRequiredService<IGitHubAppService>() as TestGitHubAppService;
        Assert.NotNull(github);
        github.InstallationException = new GitHubApiException(
            HttpStatusCode.NotFound, """{"message":"Not Found"}""");
        var link = await GetInstallLinkAsync();

        var response = await _client.PostAsJsonAsync("/api/integrations/github/connect", new
        {
            installationId = 5678,
            state = link.State
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("finns inte längre", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Status_AfterAppIsUninstalledOnGitHub_AllowsANewInstallation()
    {
        await ResetAsync();
        var link = await GetInstallLinkAsync();
        await _client.PostAsJsonAsync("/api/integrations/github/connect", new
        {
            installationId = 5678,
            state = link.State
        });

        var github = Assert.IsType<TestGitHubAppService>(
            factory.Services.GetRequiredService<IGitHubAppService>());
        github.InstallationException = new GitHubApiException(
            HttpStatusCode.NotFound, """{"message":"Not Found"}""");

        var status = await _client.GetFromJsonAsync<GitHubStatusResponse>(
            "/api/integrations/github");
        Assert.NotNull(status);
        Assert.False(status.Connected);
        Assert.Equal(0, status.InstallationId);
        Assert.Contains("finns inte längre", status.Error);

        // Efter ominstallation på GitHub ska en helt ny installation kunna kopplas.
        github.InstallationException = null;
        github.Installation = new GitHubInstallation(
            9999, "skol-org", "Organization", 1234, TestGitHubAppService.FullPermissions);
        var newLink = await GetInstallLinkAsync();
        var response = await _client.PostAsJsonAsync("/api/integrations/github/connect", new
        {
            installationId = 9999,
            state = newLink.State
        });

        response.EnsureSuccessStatusCode();
        var reconnected = await response.Content.ReadFromJsonAsync<GitHubStatusResponse>();
        Assert.NotNull(reconnected);
        Assert.True(reconnected.Connected);
        Assert.Equal(9999, reconnected.InstallationId);
    }

    private async Task<InstallLinkResponse> GetInstallLinkAsync()
    {
        var link = await _client.GetFromJsonAsync<InstallLinkResponse>("/api/integrations/github/install");
        return Assert.IsType<InstallLinkResponse>(link);
    }

    private async Task ResetAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        db.SchoolGitHubInstallations.RemoveRange(db.SchoolGitHubInstallations);
        await db.SaveChangesAsync();

        var github = factory.Services.GetRequiredService<IGitHubAppService>() as TestGitHubAppService;
        Assert.NotNull(github);
        github.Installation = new GitHubInstallation(
            5678,
            "skol-org",
            "Organization",
            1234,
            TestGitHubAppService.FullPermissions);
        github.InstallationException = null;
    }
}
