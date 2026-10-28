using System.Net;
using System.Net.Http.Json;

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
}
