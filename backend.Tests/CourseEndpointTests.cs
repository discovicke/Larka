using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class CourseEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record CourseResponse(Guid Id, Guid SchoolId, string Title, string Code);

    [Fact]
    public async Task CreateCourse_ReturnsCreated_AndShowsInList()
    {
        var schoolId = PlaceholderIds.SchoolId;

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
        var schoolId = PlaceholderIds.SchoolId;
        var code = $"K{Guid.NewGuid():N}"[..10];
        await AddCourseAtOtherSchoolAsync(code);

        var first = await _client.PostAsJsonAsync("/api/courses", new { schoolId, title = "Första", code });
        var sameSchool = await _client.PostAsJsonAsync("/api/courses", new { schoolId, title = "Andra", code });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, sameSchool.StatusCode);
    }

    [Fact]
    public async Task CreateCourse_AtAnotherSchool_ReturnsBadRequest_EvenForAdmin()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/courses", new { schoolId = await AddSchoolAsync(), title = "DevOps", code = "DEVOPS" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("school does not exist", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", "KOD", "title is required")]
    [InlineData("Titel", " ", "code is required")]
    public async Task CreateCourse_WithMissingField_ReturnsBadRequest(string title, string code, string error)
    {
        var schoolId = PlaceholderIds.SchoolId;

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

    [Fact]
    public async Task ListCourses_ShowsOnlyTheCallersSchool()
    {
        var (own, other, _) = await AddCoursesInTwoSchoolsAsync();

        var asAdmin = await _client.GetFromJsonAsync<List<CourseResponse>>("/api/courses");

        Assert.Contains(asAdmin!, item => item.Id == own.Id);
        Assert.DoesNotContain(asAdmin!, item => item.Id == other.Id);
        Assert.All(asAdmin!, item => Assert.Equal(PlaceholderIds.SchoolId, item.SchoolId));
    }

    [Fact]
    public async Task ListCourses_AsUserAtAnotherSchool_ShowsThatSchoolOnly()
    {
        var (own, other, otherUserId) = await AddCoursesInTwoSchoolsAsync();

        var asOtherUser = await GetAsync("/api/courses", otherUserId);

        var courses = await asOtherUser.Content.ReadFromJsonAsync<List<CourseResponse>>();
        Assert.Equal([other.Id], courses!.Select(item => item.Id));
        Assert.DoesNotContain(courses!, item => item.Id == own.Id);
    }

    [Fact]
    public async Task GetCourse_AtAnotherSchool_Returns404()
    {
        var (own, other, _) = await AddCoursesInTwoSchoolsAsync();

        var ownResponse = await _client.GetAsync($"/api/courses/{own.Id}");
        var otherResponse = await _client.GetAsync($"/api/courses/{other.Id}");
        var staffResponse = await _client.GetAsync($"/api/courses/{other.Id}/staff");

        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, staffResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateCourse_AtAnotherSchool_ReturnsNotFound_EvenForAdmin()
    {
        var (_, other, _) = await AddCoursesInTwoSchoolsAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/courses/{other.Id}",
            new
            {
                title = "Ny titel",
                description = "",
                points = (int?)null,
                startsOn = (string?)null,
                endsOn = (string?)null
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }

    // En kurs i adminens skola, en kurs i en annan skola och en användare i den andra skolan.
    private async Task<(Course Own, Course Other, Guid OtherUserId)> AddCoursesInTwoSchoolsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var otherSchool = await TestData.AddSchoolAsync(db);
        var own = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId);
        var other = await TestData.AddCourseAsync(db, otherSchool.Id);
        var otherUser = await TestData.AddUserAsync(db, otherSchool.Id, UserRole.Student);
        return (own, other, otherUser.Id);
    }

    private async Task AddCourseAtOtherSchoolAsync(string code)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        db.Courses.Add(new Course { SchoolId = school.Id, Title = "Annan skola", Code = code });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddSchoolAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return (await TestData.AddSchoolAsync(db)).Id;
    }
}
