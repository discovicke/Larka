using System.Net.Http.Json;
using Backend.Api.Endpoints;

namespace backend.Tests;

public class VersionEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public VersionEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetVersion_WithoutDeployInfo_ReturnsDefaults()
    {
        var version = await _factory.CreateClient().GetFromJsonAsync<VersionResponse>("/version");

        Assert.Equal(new VersionResponse("dev", "local"), version);
    }

    [Fact]
    public async Task GetVersion_ReturnsDeployedVersionAndCommit()
    {
        var client = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("APP_VERSION", "1.2.3");
            b.UseSetting("GIT_COMMIT", "abc123");
        }).CreateClient();

        var version = await client.GetFromJsonAsync<VersionResponse>("/version");

        Assert.Equal(new VersionResponse("1.2.3", "abc123"), version);
    }
}
