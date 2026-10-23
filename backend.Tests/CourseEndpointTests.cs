using System.Net;
using System.Net.Http.Json;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class CourseEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record CourseResponse(Guid Id, Guid SchoolId, string Title, string Code);

    [Fact]
    public async Task CreateCourse_ReturnsCreated_AndShowsInList()
    {
        var schoolId = await AddSchoolAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/courses", new { schoolId, title = " DevOps ", code = " DEVOPS25 " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var course = await response.Content.ReadFromJsonAsync<CourseResponse>();
        Assert.Equal(("DevOps", "DEVOPS25"), (course!.Title, course.Code));

        var found = await _client.GetFromJsonAsync<CourseResponse>($"/api/courses/{course.Id}");
        var all = await _client.GetFromJsonAsync<List<CourseResponse>>("/api/courses");
        Assert.Equal(schoolId, found!.SchoolId);
        Assert.Contains(all!, item => item.Id == course.Id);
    }

    [Fact]
    public async Task CreateCourse_WithSameCodeAtSameSchool_ReturnsBadRequest()
    {
        var schoolId = await AddSchoolAsync();
        await _client.PostAsJsonAsync("/api/courses", new { schoolId, title = "Första", code = "SAMMA" });

        var sameSchool = await _client.PostAsJsonAsync(
            "/api/courses", new { schoolId, title = "Andra", code = "SAMMA" });
        var otherSchool = await _client.PostAsJsonAsync(
            "/api/courses", new { schoolId = await AddSchoolAsync(), title = "Andra", code = "SAMMA" });

        Assert.Equal(HttpStatusCode.BadRequest, sameSchool.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherSchool.StatusCode);
    }

    [Theory]
    [InlineData("", "KOD", "title is required")]
    [InlineData("Titel", " ", "code is required")]
    public async Task CreateCourse_WithMissingField_ReturnsBadRequest(string title, string code, string error)
    {
        var schoolId = await AddSchoolAsync();

        var response = await _client.PostAsJsonAsync("/api/courses", new { schoolId, title, code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(error, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateCourse_AtSchoolThatDoesNotExist_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/courses", new { schoolId = Guid.NewGuid(), title = "DevOps", code = "DEVOPS" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("school does not exist", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetCourse_ThatDoesNotExist_Returns404()
    {
        var response = await _client.GetAsync($"/api/courses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> AddSchoolAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return (await TestData.AddSchoolAsync(db)).Id;
    }
}
