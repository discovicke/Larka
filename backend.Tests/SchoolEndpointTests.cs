using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Enums;
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

    // #521: admin i skolan byter namn på skolan. Andra får 403, en annan skola 404.
    [Fact]
    public async Task UpdateSchool_AsAdminOfTheSchool_RenamesIt()
    {
        var (schoolId, adminId, teacherId) = await AddSchoolWithAdminAndTeacherAsync();

        var renamed = await SendAsync(
            HttpMethod.Put, $"/api/schools/{schoolId}", adminId, new { name = "  Lärkskolan  " });
        var read = await GetAsync($"/api/schools/{schoolId}", adminId);
        var empty = await SendAsync(HttpMethod.Put, $"/api/schools/{schoolId}", adminId, new { name = " " });
        var tooLong = await SendAsync(
            HttpMethod.Put, $"/api/schools/{schoolId}", adminId, new { name = new string('a', 201) });
        var asTeacher = await SendAsync(HttpMethod.Put, $"/api/schools/{schoolId}", teacherId, new { name = "Nej" });
        var otherSchool = await SendAsync(
            HttpMethod.Put, $"/api/schools/{PlaceholderIds.SchoolId}", adminId, new { name = "Nej" });

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Lärkskolan", (await renamed.Content.ReadFromJsonAsync<SchoolResponse>())!.Name);
        Assert.Equal("Lärkskolan", (await read.Content.ReadFromJsonAsync<SchoolResponse>())!.Name);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("name is required", await empty.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asTeacher.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherSchool.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Guid userId, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<(Guid SchoolId, Guid AdminId, Guid TeacherId)> AddSchoolWithAdminAndTeacherAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var admin = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
        var teacher = await TestData.AddUserAsync(db, school.Id, UserRole.Teacher);
        return (school.Id, admin.Id, teacher.Id);
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
