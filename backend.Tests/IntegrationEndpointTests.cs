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
        List<string> MissingSettings);

    private record InstallLinkResponse(string Url, string State);

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
