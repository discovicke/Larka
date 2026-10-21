using System.Net;

namespace backend.Tests;

/// <summary>
/// Integrationstester som snurrar upp hela appen i minnet och gör riktiga HTTP-anrop.
/// </summary>
public class HealthEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_ReturnsOkText()
    {
        var body = await _client.GetStringAsync("/health");

        Assert.Equal("OK", body);
    }
}
