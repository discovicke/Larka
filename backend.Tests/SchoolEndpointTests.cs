using System.Net;
using System.Net.Http.Json;

namespace backend.Tests;

public class SchoolEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record SchoolResponse(Guid Id, string Name);

    [Fact]
    public async Task CreateSchool_ReturnsCreated_AndShowsInList()
    {
        var response = await _client.PostAsJsonAsync("/api/schools", new { name = "  Edugrade  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var school = await response.Content.ReadFromJsonAsync<SchoolResponse>();
        Assert.Equal("Edugrade", school!.Name);
        Assert.Equal($"/api/schools/{school.Id}", response.Headers.Location!.ToString());

        var all = await _client.GetFromJsonAsync<List<SchoolResponse>>("/api/schools");
        Assert.Contains(all!, item => item.Id == school.Id);
    }

    [Fact]
    public async Task CreateSchool_WithoutName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/schools", new { name = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name is required", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetSchool_ReturnsSchool_Or404()
    {
        var created = await _client.PostAsJsonAsync("/api/schools", new { name = "Hermods" });
        var school = await created.Content.ReadFromJsonAsync<SchoolResponse>();

        var found = await _client.GetFromJsonAsync<SchoolResponse>($"/api/schools/{school!.Id}");
        var missing = await _client.GetAsync($"/api/schools/{Guid.NewGuid()}");

        Assert.Equal("Hermods", found!.Name);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
