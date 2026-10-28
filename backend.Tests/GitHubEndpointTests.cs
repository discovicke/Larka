using System.Net;
using System.Net.Http.Json;
using Backend.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class GitHubEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record AuthorizeResponse(string Url);
    private sealed record LinkResponse(bool Linked, string? GitHubLogin);

    [Fact]
    public async Task Authorize_WhenConfigured_ReturnsUrlAndSetsCookies()
    {
        var oauth = (TestGitHubOAuthService)factory.Services.GetRequiredService<IGitHubOAuthService>();
        oauth.IsConfigured = true;

        var response = await _client.GetAsync("/api/github/authorize");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthorizeResponse>();
        Assert.NotNull(body);
        Assert.StartsWith("https://github.com/login/oauth/authorize?state=", body.Url);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(cookies, cookie => cookie.Contains("github_oauth_state="));
        Assert.Contains(cookies, cookie => cookie.Contains("github_oauth_user="));
    }

    [Fact]
    public async Task Authorize_WhenNotConfigured_ReturnsBadRequest()
    {
        var oauth = (TestGitHubOAuthService)factory.Services.GetRequiredService<IGitHubOAuthService>();
        oauth.IsConfigured = false;
        try
        {
            var response = await _client.GetAsync("/api/github/authorize");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("inte konfigurerad", content);
        }
        finally
        {
            oauth.IsConfigured = true;
        }
    }

    [Fact]
    public async Task GetLink_AndUnlink_WorksForCurrentUser()
    {
        var response = await _client.GetFromJsonAsync<LinkResponse>("/api/github/link");
        Assert.NotNull(response);

        var deleteResponse = await _client.DeleteAsync("/api/github/link");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}
