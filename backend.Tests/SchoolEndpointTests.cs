using System.Net;
using System.Net.Http.Json;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class SchoolEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Microsoft_WithoutAuthEntra_IsNotConfigured_AndCannotBeLinked()
    {
        var status = await _client.GetStringAsync($"/api/schools/{PlaceholderIds.SchoolId}/microsoft");
        var link = await _client.PutAsync($"/api/schools/{PlaceholderIds.SchoolId}/microsoft", null);

        Assert.Contains("\"configured\":false", status);
        Assert.Equal(HttpStatusCode.NotFound, link.StatusCode);
    }

    private record SchoolResponse(Guid Id, string Name);

    [Fact]
    public async Task CreateSchool_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/schools", new { name = "  Edugrade  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var school = await response.Content.ReadFromJsonAsync<SchoolResponse>();
        Assert.Equal("Edugrade", school!.Name);
        Assert.Equal($"/api/schools/{school.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task CreateSchool_WithoutName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/schools", new { name = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name is required", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetSchool_ReturnsOwnSchool_Or404()
    {
        var found = await _client.GetFromJsonAsync<SchoolResponse>($"/api/schools/{PlaceholderIds.SchoolId}");
        var missing = await _client.GetAsync($"/api/schools/{Guid.NewGuid()}");

        Assert.Equal(PlaceholderIds.SchoolId, found!.Id);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task ListSchools_ShowsOnlyTheCallersSchool()
    {
        var (otherSchoolId, otherUserId) = await AddOtherSchoolWithUserAsync();

        var asAdmin = await _client.GetFromJsonAsync<List<SchoolResponse>>("/api/schools");
        var asOtherUser = await GetAsync("/api/schools", otherUserId);

        Assert.Equal([PlaceholderIds.SchoolId], asAdmin!.Select(item => item.Id));
        var schools = await asOtherUser.Content.ReadFromJsonAsync<List<SchoolResponse>>();
        Assert.Equal([otherSchoolId], schools!.Select(item => item.Id));
    }

    [Fact]
    public async Task GetSchool_AtAnotherSchool_Returns404_EvenForAdmin()
    {
        var (otherSchoolId, otherUserId) = await AddOtherSchoolWithUserAsync();

        var asAdmin = await _client.GetAsync($"/api/schools/{otherSchoolId}");
        var asOtherUser = await GetAsync($"/api/schools/{PlaceholderIds.SchoolId}", otherUserId);

        Assert.Equal(HttpStatusCode.NotFound, asAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asOtherUser.StatusCode);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<(Guid SchoolId, Guid UserId)> AddOtherSchoolWithUserAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var user = await TestData.AddUserAsync(db, school.Id);
        return (school.Id, user.Id);
    }
}
