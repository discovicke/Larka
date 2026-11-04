using System.Net;
using System.Net.Http.Json;
using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Läraren i en kurs är den som står i CourseTeachers. Klassen har elever och en mentor, och att vara lärare i
// klassen ger ingen behörighet i klassens kurser.
public class CourseTeacherEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private static readonly object _editBody = new
    {
        title = "Ny titel",
        description = "",
        points = (int?)null,
        startsOn = (string?)null,
        endsOn = (string?)null
    };

    private readonly HttpClient _client = factory.CreateClient();

    private record TeacherResponse(Guid UserId, string Name, string Email);

    private record GroupResponse(Guid Id);

    private record CourseResponse(Guid Id);

    private record MemberResponse(Guid UserId, string Role);

    private record MeResponse(List<CourseRoleResponse> Courses);

    private record CourseRoleResponse(Guid CourseId, string Role);

    // En klass med två kurser, A och B, i adminens skola. Läraren undervisar i A och är dessutom lärare i klassen.
    private record Scenario(
        Guid GroupId, Guid CourseA, Guid CourseB, Guid Teacher, Guid ClassOnlyTeacher, Guid Student);

    [Fact]
    public async Task TeacherOfCourseA_CannotEditOrGradeCourseB_InTheSameClass()
    {
        var scenario = await AddScenarioAsync();
        var linkA = await LinkOnSiteAssignmentAsync(scenario.CourseA);
        var linkB = await LinkOnSiteAssignmentAsync(scenario.CourseB);
        var submissionA = await SubmissionOfAsync(linkA, scenario.Student);
        var submissionB = await SubmissionOfAsync(linkB, scenario.Student);
        var edit = _editBody;

        var editA = await SendAsync(HttpMethod.Put, $"/api/courses/{scenario.CourseA}", scenario.Teacher, edit);
        var editB = await SendAsync(HttpMethod.Put, $"/api/courses/{scenario.CourseB}", scenario.Teacher, edit);
        var weekA = await SendAsync(
            HttpMethod.Post, $"/api/courses/{scenario.CourseA}/modules", scenario.Teacher, new { title = "V1" });
        var weekB = await SendAsync(
            HttpMethod.Post, $"/api/courses/{scenario.CourseB}/modules", scenario.Teacher, new { title = "V1" });
        var grade = new { level = "pass", feedback = "Bra" };
        var gradeB = await SendAsync(
            HttpMethod.Post, $"/api/submissions/{submissionB}/grade", scenario.Teacher, grade);
        var gradeA = await SendAsync(
            HttpMethod.Post, $"/api/submissions/{submissionA}/grade", scenario.Teacher, grade);

        Assert.Equal(HttpStatusCode.OK, editA.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, editB.StatusCode);
        Assert.Equal(HttpStatusCode.Created, weekA.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, weekB.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, gradeB.StatusCode);
        Assert.Equal(HttpStatusCode.OK, gradeA.StatusCode);
    }

    [Fact]
    public async Task TeacherOfTheClassButNotOfTheCourse_CannotEditOrGrade()
    {
        var scenario = await AddScenarioAsync();
        var link = await LinkOnSiteAssignmentAsync(scenario.CourseA);
        var submission = await SubmissionOfAsync(link, scenario.Student);

        var edit = await SendAsync(
            HttpMethod.Put, $"/api/courses/{scenario.CourseA}", scenario.ClassOnlyTeacher,
            _editBody);
        var grade = await SendAsync(
            HttpMethod.Post, $"/api/submissions/{submission}/grade", scenario.ClassOnlyTeacher,
            new { level = "pass", feedback = "Bra" });

        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, grade.StatusCode);
    }

    [Fact]
    public async Task CourseTeacherWhoIsNotInTheClass_CanEditAndGrade_AndSeesTheCourse()
    {
        var scenario = await AddScenarioAsync();
        var outside = await AddCourseTeacherOutsideAsync(scenario.CourseA);
        var link = await LinkOnSiteAssignmentAsync(scenario.CourseA);
        var submission = await SubmissionOfAsync(link, scenario.Student);

        var edit = await SendAsync(
            HttpMethod.Put, $"/api/courses/{scenario.CourseA}", outside,
            _editBody);
        var grade = await SendAsync(
            HttpMethod.Post, $"/api/submissions/{submission}/grade", outside, new { level = "pass", feedback = "Bra" });
        var courses = await GetAsync<List<CourseResponse>>("/api/courses", outside);
        var me = await GetAsync<MeResponse>("/api/me", outside);

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, grade.StatusCode);
        Assert.Equal([scenario.CourseA], courses.Select(course => course.Id));
        var role = Assert.Single(me.Courses);
        Assert.Equal((scenario.CourseA, "Teacher"), (role.CourseId, role.Role));
    }

    [Fact]
    public async Task ClassTeacher_SeesOnlyTheCoursesTheyTeach_NotEveryCourseTheClassReads()
    {
        var scenario = await AddScenarioAsync();

        var teacherCourses = await GetAsync<List<CourseResponse>>("/api/courses", scenario.Teacher);
        var teacherMe = await GetAsync<MeResponse>("/api/me", scenario.Teacher);
        var classOnlyCourses = await GetAsync<List<CourseResponse>>("/api/courses", scenario.ClassOnlyTeacher);
        var classOnlyMe = await GetAsync<MeResponse>("/api/me", scenario.ClassOnlyTeacher);
        var studentCourses = await GetAsync<List<CourseResponse>>("/api/courses", scenario.Student);

        Assert.Equal([scenario.CourseA], teacherCourses.Select(course => course.Id));
        Assert.Equal([scenario.CourseA], teacherMe.Courses.Select(course => course.CourseId));
        Assert.Empty(classOnlyCourses);
        Assert.Empty(classOnlyMe.Courses);
        Assert.Equal(
            new[] { scenario.CourseA, scenario.CourseB }.Order(),
            studentCourses.Select(course => course.Id).Order());
    }

    [Fact]
    public async Task ClassTeacher_KeepsTheGroup_ButTheCalendarOnlyHasTheCoursesTheyTeach()
    {
        var scenario = await AddScenarioAsync();
        var due = DateTimeOffset.UtcNow.AddDays(3);
        var linkA = await LinkOnSiteAssignmentAsync(scenario.CourseA, due);
        var linkB = await LinkOnSiteAssignmentAsync(scenario.CourseB, due);

        var calendarPath = "/api/calendar?from=" + Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))
            + "&to=" + Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(10).ToString("O"));
        var groups = await GetAsync<List<GroupResponse>>("/api/groups", scenario.ClassOnlyTeacher);
        var calendar = await GetAsync<System.Text.Json.JsonElement>(
            calendarPath, scenario.ClassOnlyTeacher);
        var teacherCalendar = await GetAsync<System.Text.Json.JsonElement>(
            calendarPath, scenario.Teacher);

        Assert.Equal([scenario.GroupId], groups.Select(group => group.Id));
        Assert.DoesNotContain(linkA.ToString(), calendar.GetRawText());
        Assert.DoesNotContain(linkB.ToString(), calendar.GetRawText());
        Assert.Contains(linkA.ToString(), teacherCalendar.GetRawText());
        Assert.DoesNotContain(linkB.ToString(), teacherCalendar.GetRawText());
    }

    [Fact]
    public async Task Roster_IsOpenToTheTeacherOfACourseTheGroupReads_AndToNobodyElseOutsideTheGroup()
    {
        var scenario = await AddScenarioAsync();
        var outside = await AddCourseTeacherOutsideAsync(scenario.CourseB);
        await using var db = OpenDb();
        var otherGroup = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var otherCourse = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, otherGroup.Id);
        var teacherOfOther = await TestData.AddCourseTeacherAsync(db, otherCourse);

        var byCourseTeacher = await SendAsync(HttpMethod.Get, $"/api/groups/{scenario.GroupId}/members", outside);
        var byTeacherOfOther = await SendAsync(
            HttpMethod.Get, $"/api/groups/{scenario.GroupId}/members", teacherOfOther.Id);
        var groups = await GetAsync<List<GroupResponse>>("/api/groups", outside);

        Assert.Equal(HttpStatusCode.OK, byCourseTeacher.StatusCode);
        var members = (await byCourseTeacher.Content.ReadFromJsonAsync<List<MemberResponse>>())!;
        Assert.Contains(members, member => member.UserId == scenario.Student && member.Role == "Student");
        Assert.Equal(HttpStatusCode.Forbidden, byTeacherOfOther.StatusCode);
        Assert.Equal([scenario.GroupId], groups.Select(group => group.Id));
    }

    [Fact]
    public async Task Roster_IsClosedAgainWhenTheTeacherIsRemovedFromTheCourse()
    {
        var scenario = await AddScenarioAsync();
        var outside = await AddCourseTeacherOutsideAsync(scenario.CourseA);

        var removed = await SendAsync(
            HttpMethod.Delete, $"/api/courses/{scenario.CourseA}/teachers/{outside}", Guid.Empty);
        var roster = await SendAsync(HttpMethod.Get, $"/api/groups/{scenario.GroupId}/members", outside);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, roster.StatusCode);
    }

    [Fact]
    public async Task ListTeachers_AsAdmin_ListsTheCourseTeachersByName()
    {
        var scenario = await AddScenarioAsync(placeholderTeaches: false);
        var outside = await AddCourseTeacherOutsideAsync(scenario.CourseA);

        var teachers = await GetAsync<List<TeacherResponse>>($"/api/courses/{scenario.CourseA}/teachers", Guid.Empty);

        Assert.Equal(
            new[] { scenario.Teacher, outside }.Order(),
            teachers.Select(teacher => teacher.UserId).Order());
        Assert.Empty(await GetAsync<List<TeacherResponse>>($"/api/courses/{scenario.CourseB}/teachers", Guid.Empty));
    }

    [Fact]
    public async Task Teachers_AsTeacherOrStudent_AreForbidden_OnAllThree()
    {
        var scenario = await AddScenarioAsync();
        var target = (await AddUserAsync(UserRole.Teacher)).Id;
        var path = $"/api/courses/{scenario.CourseA}/teachers";

        foreach (var user in new[] { scenario.Teacher, scenario.ClassOnlyTeacher, scenario.Student })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, path, user)).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await SendAsync(HttpMethod.Post, path, user, new { userId = target })).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await SendAsync(HttpMethod.Delete, $"{path}/{scenario.Teacher}", user)).StatusCode);
        }

        await using var db = OpenDb();
        Assert.False(await db.CourseTeachers.AnyAsync(row => row.UserId == target));
        Assert.True(await db.CourseTeachers.AnyAsync(row => row.UserId == scenario.Teacher));
    }

    [Fact]
    public async Task AddTeacher_AsAdmin_AddsATeacher_AndGivesAccess()
    {
        var scenario = await AddScenarioAsync();
        var teacher = await AddUserAsync(UserRole.Teacher);

        var addedTeacher = await SendAsync(
            HttpMethod.Post, $"/api/courses/{scenario.CourseB}/teachers", Guid.Empty, new { userId = teacher.Id });

        Assert.Equal(HttpStatusCode.Created, addedTeacher.StatusCode);
        var body = (await addedTeacher.Content.ReadFromJsonAsync<TeacherResponse>())!;
        Assert.Equal((teacher.Id, teacher.Name, teacher.Email), (body.UserId, body.Name, body.Email));
        var edit = await SendAsync(
            HttpMethod.Put, $"/api/courses/{scenario.CourseB}", teacher.Id,
            _editBody);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
    }

    [Fact]
    public async Task AddTeacher_WithInvalidTarget_IsRejected()
    {
        var scenario = await AddScenarioAsync();
        var path = $"/api/courses/{scenario.CourseA}/teachers";
        var otherSchool = await AddUserInOtherSchoolAsync(UserRole.Teacher);
        var student = await AddUserAsync(UserRole.Student);
        var admin = await AddUserAsync(UserRole.Admin);

        var foreign = await SendAsync(HttpMethod.Post, path, Guid.Empty, new { userId = otherSchool });
        var asStudent = await SendAsync(HttpMethod.Post, path, Guid.Empty, new { userId = student.Id });
        // Admin undervisar aldrig, och inte heller admin som lägger till sig själv.
        var asAdmin = await SendAsync(HttpMethod.Post, path, Guid.Empty, new { userId = admin.Id });
        var asSelf = await SendAsync(
            HttpMethod.Post, $"/api/courses/{scenario.CourseB}/teachers", Guid.Empty,
            new { userId = PlaceholderIds.UserId });
        var unknown = await SendAsync(HttpMethod.Post, path, Guid.Empty, new { userId = Guid.NewGuid() });
        var duplicate = await SendAsync(HttpMethod.Post, path, Guid.Empty, new { userId = scenario.Teacher });
        var noCourse = await SendAsync(
            HttpMethod.Post, $"/api/courses/{Guid.NewGuid()}/teachers", Guid.Empty, new { userId = student.Id });

        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, asStudent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, asAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, asSelf.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noCourse.StatusCode);
        await using var db = OpenDb();
        Assert.False(await db.CourseTeachers.AnyAsync(
            row => row.UserId == otherSchool || row.UserId == student.Id || row.UserId == admin.Id));
    }

    [Fact]
    public async Task Teachers_OfACourseAtAnotherSchool_ReturnNotFound_EvenForAdmin()
    {
        await using var db = OpenDb();
        var school = await TestData.AddSchoolAsync(db);
        var course = await TestData.AddCourseAsync(db, school.Id);
        var teacher = await TestData.AddCourseTeacherAsync(db, course);
        var path = $"/api/courses/{course.Id}/teachers";

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, path, Guid.Empty)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Post, path, Guid.Empty, new { userId = teacher.Id })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(HttpMethod.Delete, $"{path}/{teacher.Id}", Guid.Empty)).StatusCode);
    }

    [Fact]
    public async Task RemoveTeacher_OfAUserWhoIsNotATeacher_ReturnsNotFound()
    {
        var scenario = await AddScenarioAsync();

        var response = await SendAsync(
            HttpMethod.Delete, $"/api/courses/{scenario.CourseA}/teachers/{scenario.Student}", Guid.Empty);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LinkToOnlyAGroup_StillUsesTheTeacherRoleInTheGroup_ButALinkWithACourseDoesNot()
    {
        var scenario = await AddScenarioAsync();
        var groupLink = await LinkNewsAsync(new { groupId = scenario.GroupId });
        var courseLink = await LinkNewsAsync(new { groupId = scenario.GroupId, courseId = scenario.CourseB });
        var moduleContent = await AddModuleContentAsync(scenario.CourseB);
        using var scope = factory.Services.CreateScope();
        var submissions = scope.ServiceProvider.GetRequiredService<ISubmissionRepository>();
        var contents = scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var ct = CancellationToken.None;

        // Läraren i klassen, som inte är lärare i kurs B.
        Assert.True(await submissions.IsTeacherOfLinkAsync(groupLink.Link, scenario.ClassOnlyTeacher, ct));
        Assert.True(await contents.IsTeacherOfContentAsync(groupLink.Content, scenario.ClassOnlyTeacher, ct));
        Assert.False(await submissions.IsTeacherOfLinkAsync(courseLink.Link, scenario.ClassOnlyTeacher, ct));
        Assert.False(await contents.IsTeacherOfContentAsync(courseLink.Content, scenario.ClassOnlyTeacher, ct));
        Assert.False(await contents.IsTeacherOfContentAsync(moduleContent, scenario.ClassOnlyTeacher, ct));

        // Kursläraren, som inte är lärare i klassen. Kurs A ligger i samma klass.
        var outside = await AddCourseTeacherOutsideAsync(scenario.CourseB);
        Assert.False(await submissions.IsTeacherOfLinkAsync(groupLink.Link, outside, ct));
        Assert.True(await submissions.IsTeacherOfLinkAsync(courseLink.Link, outside, ct));
        Assert.True(await contents.IsTeacherOfContentAsync(courseLink.Content, outside, ct));
        Assert.True(await contents.IsTeacherOfContentAsync(moduleContent, outside, ct));
    }

    // Klassen läser kurs A och B. Läraren undervisar bara i A, klasslärare är lärare i klassen och inte i någon kurs.
    // Med placeholderTeaches undervisar testernas standardanvändare (admin) i kurserna och i klassen, så att
    // innehåll kan publiceras som placeholder. Utan är admin bara admin.
    private async Task<Scenario> AddScenarioAsync(bool placeholderTeaches = true)
    {
        await using var db = OpenDb();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId, placeholderTeaches);
        var courseA = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, group.Id, placeholderTeaches);
        var courseB = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, group.Id, placeholderTeaches);
        var student = await TestData.AddMemberAsync(db, group);
        var teacher = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);
        var classOnly = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);
        await TestData.AddCourseTeacherAsync(db, courseA, teacher);
        return new Scenario(group.Id, courseA.Id, courseB.Id, teacher.Id, classOnly.Id, student.Id);
    }

    private async Task<Guid> AddCourseTeacherOutsideAsync(Guid courseId)
    {
        await using var db = OpenDb();
        var course = await db.Courses.SingleAsync(found => found.Id == courseId);
        return (await TestData.AddCourseTeacherAsync(db, course)).Id;
    }

    private async Task<User> AddUserAsync(UserRole role)
    {
        await using var db = OpenDb();
        return await TestData.AddUserAsync(db, PlaceholderIds.SchoolId, role);
    }

    private async Task<Guid> AddUserInOtherSchoolAsync(UserRole role)
    {
        await using var db = OpenDb();
        return (await TestData.AddUserAsync(db, role: role)).Id;
    }

    // En uppgift som bedöms på plats, delad ut till kursen. Utdelningen ger en inlämningsrad per elev i klassen.
    private async Task<Guid> LinkOnSiteAssignmentAsync(Guid courseId, DateTimeOffset? dueAt = null)
    {
        var contentId = (await _client.PostJsonAsync<IdResponse>("/api/content", new
        {
            type = "assignment",
            title = "Redovisning",
            bodyMd = "",
            gradeScale = "passFail",
            submissionModes = new[] { "onSite" }
        })).Id;
        return (await _client.PostJsonAsync<IdResponse>($"/api/content/{contentId}/links", new { courseId, dueAt })).Id;
    }

    private record LinkedNews(Guid Content, Guid Link);

    private async Task<LinkedNews> LinkNewsAsync(object link)
    {
        var contentId = (await _client.PostJsonAsync<IdResponse>(
            "/api/content", new { type = "news", title = "Nyhet", bodyMd = "Text" })).Id;
        var response = await _client.PostAsJsonAsync($"/api/content/{contentId}/links", link);
        response.EnsureSuccessStatusCode();
        return new LinkedNews(contentId, (await response.Content.ReadFromJsonAsync<IdResponse>())!.Id);
    }

    // Innehåll som bara är kopplat till en vecka i kursen.
    private async Task<Guid> AddModuleContentAsync(Guid courseId)
    {
        await using var db = OpenDb();
        var module = new Module { CourseId = courseId, Title = "Vecka 1", Position = 1 };
        var content = new Content
        {
            AuthorId = PlaceholderIds.UserId,
            Type = ContentType.Theory,
            Title = "Teori i veckan"
        };
        db.AddRange(module, content);
        db.ContentLinks.Add(new ContentLink { ContentId = content.Id, CourseId = courseId, ModuleId = module.Id });
        await db.SaveChangesAsync();
        return content.Id;
    }

    private async Task<Guid> SubmissionOfAsync(Guid linkId, Guid studentId)
    {
        await using var db = OpenDb();
        return (await db.Submissions.SingleAsync(
            submission => submission.ContentLinkId == linkId && submission.StudentId == studentId)).Id;
    }

    private async Task<T> GetAsync<T>(string path, Guid asUser)
    {
        var response = await SendAsync(HttpMethod.Get, path, asUser);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    // Guid.Empty betyder placeholder-användaren, som är Admin.
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, Guid asUser, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (asUser != Guid.Empty)
        {
            request.Headers.Add(TestAuthHandler.Header, asUser.ToString());
        }

        return await _client.SendAsync(request);
    }

    private AppDb OpenDb() => factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDb>();
}
