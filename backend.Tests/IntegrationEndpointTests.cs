using System.Net;
using System.Net.Http.Json;
using Backend.Application.Abstractions;
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

    [Fact]
    public async Task GetGitHubStatus_AsAdmin_ReturnsSanitizedStatus()
    {
        var status = await _client.GetFromJsonAsync<GitHubStatusResponse>("/api/integrations/github");

        Assert.NotNull(status);
        Assert.False(status.Configured);
        Assert.False(status.Connected);
        Assert.Equal("skol-org", status.Org);
        Assert.Contains("AppId", status.MissingSettings);
        Assert.Contains("InstallationId", status.MissingSettings);
        Assert.Contains("PrivateKey", status.MissingSettings);
        Assert.DoesNotContain("PrivateKeyPath", status.MissingSettings);
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
    public async Task Connect_WhenNotConfigured_ReturnsBadRequest()
    {
        var provider = (TestGitHubConnectionStatusProvider)factory.Services
            .GetRequiredService<IGitHubConnectionStatusProvider>();
        provider.Status = new GitHubConnectionStatus(
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

        var response = await _client.PostAsync("/api/integrations/github/connect", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("inte konfigurerad", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ConnectAndDisconnect_AsAdmin_UpdatesConnectionState()
    {
        var provider = (TestGitHubConnectionStatusProvider)factory.Services
            .GetRequiredService<IGitHubConnectionStatusProvider>();
        provider.Status = new Backend.Application.Abstractions.GitHubConnectionStatus(
            Configured: true,
            AppId: 1234,
            InstallationId: 5678,
            Org: "skol-org",
            ApiBaseUrl: "https://api.github.com/",
            Connected: true,
            AccountLogin: "skol-org",
            LastVerifiedAt: DateTimeOffset.UtcNow,
            Error: null,
            MissingSettings: []);

        var connectResponse = await _client.PostAsync("/api/integrations/github/connect", null);
        Assert.Equal(HttpStatusCode.OK, connectResponse.StatusCode);
        var connectedStatus = await connectResponse.Content.ReadFromJsonAsync<GitHubStatusResponse>();
        Assert.NotNull(connectedStatus);
        Assert.True(connectedStatus.Connected);

        var disconnectResponse = await _client.DeleteAsync("/api/integrations/github");
        Assert.Equal(HttpStatusCode.NoContent, disconnectResponse.StatusCode);

        var statusAfterDisconnect = await _client.GetFromJsonAsync<GitHubStatusResponse>("/api/integrations/github");
        Assert.NotNull(statusAfterDisconnect);
        Assert.False(statusAfterDisconnect.Connected);
    }
}
