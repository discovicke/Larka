using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
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
    public async Task ListCourses_ShowsStudentsAndTeachersOnlyTheCoursesTheirGroupsRead()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var ownGroup = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var otherGroup = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var own = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, ownGroup.Id);
        var other = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, otherGroup.Id);
        var unlinked = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId);
        var student = await TestData.AddMemberAsync(db, ownGroup);
        var teacher = await TestData.AddMemberAsync(db, ownGroup, GroupRole.Teacher, UserRole.Teacher);
        var outsider = await TestData.AddUserAsync(db, PlaceholderIds.SchoolId);

        var admin = await _client.GetFromJsonAsync<List<CourseResponse>>("/api/courses");
        var asStudent = await ListCoursesAsync(student.Id);
        var asTeacher = await ListCoursesAsync(teacher.Id);
        var asOutsider = await ListCoursesAsync(outsider.Id);

        Assert.All([own, other, unlinked], course => Assert.Contains(admin!, item => item.Id == course.Id));
        Assert.Equal([own.Id], asStudent.Select(item => item.Id));
        Assert.Equal([own.Id], asTeacher.Select(item => item.Id));
        Assert.Empty(asOutsider);
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

    [Fact]
    public async Task CreateCourse_WithPointsAndDates_StoresThem()
    {
        var response = await _client.PostAsJsonAsync("/api/courses", new
        {
            schoolId = PlaceholderIds.SchoolId,
            title = "DevOps",
            code = $"K{Guid.NewGuid():N}"[..10],
            points = 30,
            startsOn = "2027-01-11",
            endsOn = "2027-03-26"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"points\":30", body);
        Assert.Contains("2027-01-11", body);
    }

    [Theory]
    [InlineData(0, "2027-01-11", "2027-03-26", "points must be between 1 and 400")]
    [InlineData(30, "2027-03-26", "2027-01-11", "endsOn must be on or after startsOn")]
    public async Task CreateCourse_WithInvalidPointsOrDates_ReturnsBadRequest(
        int points, string startsOn, string endsOn, string error)
    {
        var response = await _client.PostAsJsonAsync("/api/courses", new
        {
            schoolId = PlaceholderIds.SchoolId,
            title = "DevOps",
            code = "X1",
            points,
            startsOn,
            endsOn
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(error, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateCourse_AsTeacher_ReturnsForbidden()
    {
        var teacher = await AddTeacherAsync();

        var response = await SendAsync(
            HttpMethod.Post, "/api/courses", teacher.Id,
            new { schoolId = PlaceholderIds.SchoolId, title = "DevOps", code = "NOPE" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCourse_RemovesCourseWeeksContentAndGroupLinks_ButKeepsSharedContent()
    {
        var setup = await AddCourseWithContentAsync();

        var response = await _client.DeleteAsync($"/api/courses/{setup.CourseId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/courses/{setup.CourseId}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.False(await db.Modules.AnyAsync(module => module.CourseId == setup.CourseId));
        Assert.False(await db.GroupCourses.AnyAsync(groupCourse => groupCourse.CourseId == setup.CourseId));
        Assert.False(await db.ContentLinks.AnyAsync(link => link.CourseId == setup.CourseId));
        Assert.False(await db.Content.AnyAsync(content => content.Id == setup.OnlyHereContentId));
        Assert.True(await db.Content.AnyAsync(content => content.Id == setup.SharedContentId));
        Assert.True(await db.Groups.AnyAsync(group => group.Id == setup.GroupId));
    }

    [Fact]
    public async Task DeleteCourse_WithStudentWork_ReturnsConflict_AndKeepsEverything()
    {
        var setup = await AddCourseWithContentAsync(withSubmission: true);

        var response = await _client.DeleteAsync($"/api/courses/{setup.CourseId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("student work", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/courses/{setup.CourseId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteCourse_AsTeacherOfTheCourse_ReturnsForbidden()
    {
        var setup = await AddCourseWithContentAsync();
        var teacher = await AddTeacherAsync(setup.GroupId);

        var response = await SendAsync(HttpMethod.Delete, $"/api/courses/{setup.CourseId}", teacher.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/courses/{setup.CourseId}")).StatusCode);
    }

    [Fact]
    public async Task DeleteCourse_ThatDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/courses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCourse_AtAnotherSchool_ReturnsNotFound_EvenForAdmin()
    {
        var (_, other, _) = await AddCoursesInTwoSchoolsAsync();

        var response = await _client.DeleteAsync($"/api/courses/{other.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record CourseWithContent(Guid CourseId, Guid GroupId, Guid SharedContentId, Guid OnlyHereContentId);

    // En kurs i adminens skola som en grupp läser, med en vecka och två delar: en bara här och en delad med gruppen.
    private async Task<CourseWithContent> AddCourseWithContentAsync(bool withSubmission = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var course = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, group.Id);
        var student = await TestData.AddMemberAsync(db, group, GroupRole.Student, UserRole.Student);
        var week = new Module { CourseId = course.Id, Title = "Vecka 1", Position = 1 };
        var onlyHere = new Content { AuthorId = PlaceholderIds.UserId, Type = ContentType.Theory, Title = "Bara här" };
        var shared = new Content { AuthorId = PlaceholderIds.UserId, Type = ContentType.Theory, Title = "Delad" };
        var onlyHereLink = new ContentLink
        {
            ContentId = onlyHere.Id,
            CourseId = course.Id,
            ModuleId = week.Id,
            Position = 1
        };
        db.Modules.Add(week);
        db.Content.AddRange(onlyHere, shared);
        db.ContentLinks.AddRange(
            onlyHereLink,
            new ContentLink { ContentId = shared.Id, CourseId = course.Id, ModuleId = week.Id, Position = 2 },
            new ContentLink { ContentId = shared.Id, GroupId = group.Id });
        if (withSubmission)
        {
            db.Submissions.Add(new Submission
            {
                ContentLinkId = onlyHereLink.Id,
                StudentId = student.Id,
                Status = SubmissionStatus.Submitted,
                SubmittedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
        return new CourseWithContent(course.Id, group.Id, shared.Id, onlyHere.Id);
    }

    private async Task<User> AddTeacherAsync(Guid? groupId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = groupId is null
            ? await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId)
            : await db.Groups.SingleAsync(found => found.Id == groupId);
        return await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Guid userId, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<List<CourseResponse>> ListCoursesAsync(Guid userId)
    {
        var response = await GetAsync("/api/courses", userId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<CourseResponse>>())!;
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
        var otherUser = await TestData.AddUserAsync(db, otherSchool.Id, UserRole.Admin);
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
