using System.Net.Http.Json;
using Backend.Api.Endpoints;

namespace backend.Tests;

public class VersionEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task GetVersion_WithoutDeployInfo_ReturnsDefaults()
    {
        var version = await factory.CreateClient().GetFromJsonAsync<VersionResponse>("/version");

        Assert.Equal(new VersionResponse("dev", "local"), version);
    }

    [Fact]
    public async Task GetVersion_ReturnsDeployedVersionAndCommit()
    {
        var client = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("APP_VERSION", "1.2.3");
            b.UseSetting("GIT_COMMIT", "abc123");
        }).CreateClient();

        var version = await client.GetFromJsonAsync<VersionResponse>("/version");

        Assert.Equal(new VersionResponse("1.2.3", "abc123"), version);
    }
}
