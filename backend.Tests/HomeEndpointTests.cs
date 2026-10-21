using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace backend.Tests;

/// <summary>
/// Integrationstester som snurrar upp hela appen i minnet och gör riktiga HTTP-anrop.
/// </summary>
public class HomeEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HomeEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRoot_ReturnsOk()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRoot_ReturnsHelloWorld()
    {
        var body = await _client.GetStringAsync("/");

        Assert.Equal("Hello World!", body);
    }
}
