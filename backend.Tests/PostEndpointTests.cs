using System.Net;
using System.Net.Http.Json;

namespace backend.Tests;

public class PostEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PostEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreatePost_WithoutAuth_ReturnsCreated()
    {
        var groupId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync(
            $"/groups/{groupId}/posts",
            new { type = "news", title = "Välkommen", bodyMd = "Hej!" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreatePost_ThenListPosts_ReturnsCreatedPost()
    {
        var groupId = Guid.NewGuid();
        await _client.PostAsJsonAsync(
            $"/groups/{groupId}/posts",
            new { type = "theory", title = "Loopar", bodyMd = "for och while" });

        var body = await _client.GetStringAsync($"/groups/{groupId}/posts");

        Assert.Contains("Loopar", body);
    }
}
