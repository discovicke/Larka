using System.Net;

namespace backend.Tests;

public class RoutingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RoutingTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UnknownApiRoute_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApiRoutes_AreNotServedFromRoot()
    {
        var response = await _client.GetAsync($"/groups/{Guid.NewGuid()}/content");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
