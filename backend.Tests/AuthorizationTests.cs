using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Behörigheterna prövas i API:et: eleven får 403, läraren på kursen får göra det, läraren på en annan
// kurs får 403 och admin får inte undervisa (förrän hen också står som lärare i kursen). Anropen görs som
// användaren i X-Test-User. Admin bestämmer vem som undervisar, läraren bestämmer vad som händer i kursen.
public class AuthorizationTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record Scenario(
        Guid SchoolId,
        Guid GroupId,
        Guid CourseId,
        Guid OtherCourseId,
        Guid StudentId,
        Guid TeacherId,
        Guid OtherTeacherId,
        Guid AdminId,
        Guid AdminTeacherId);

    [Fact]
    public async Task CreateModule_OnlyTheTeacherOfTheCourseSucceeds_AdminOnlyWithATeacherRow()
    {
        var scenario = await AddScenarioAsync();

        var byStudent = await PostJsonAsync(
            $"/api/courses/{scenario.CourseId}/modules", scenario.StudentId, new { title = "Vecka 1" });
        var byOtherTeacher = await PostJsonAsync(
            $"/api/courses/{scenario.CourseId}/modules", scenario.OtherTeacherId, new { title = "Vecka 1" });
        var byTeacher = await PostJsonAsync(
            $"/api/courses/{scenario.CourseId}/modules", scenario.TeacherId, new { title = "Vecka 1" });
        var byAdmin = await PostJsonAsync(
            $"/api/courses/{scenario.CourseId}/modules", scenario.AdminId, new { title = "Vecka 2" });
        var byAdminWhoTeaches = await PostJsonAsync(
            $"/api/courses/{scenario.CourseId}/modules", scenario.AdminTeacherId, new { title = "Vecka 3" });

        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byOtherTeacher.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byTeacher.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byAdminWhoTeaches.StatusCode);
        Assert.Equal("application/problem+json", byStudent.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Du har inte behörighet", await byStudent.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateContent_OnlyWhoTeachesSomethingSucceeds_NotAdminAsSuch()
    {
        var scenario = await AddScenarioAsync();
        var content = new { type = "theory", title = "Teori", bodyMd = "Text" };

        var byStudent = await PostJsonAsync("/api/content", scenario.StudentId, content);
        var byTeacher = await PostJsonAsync("/api/content", scenario.TeacherId, content);
        var byAdmin = await PostJsonAsync("/api/content", scenario.AdminId, content);
        var byAdminWhoTeaches = await PostJsonAsync("/api/content", scenario.AdminTeacherId, content);

        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byTeacher.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byAdminWhoTeaches.StatusCode);
    }

    [Fact]
    public async Task UpdateAndPublishContent_StudentIsForbidden_TeacherOfTheLinkSucceeds()
    {
        var scenario = await AddScenarioAsync();
        var contentId = await CreateTheoryAsync();
        await _client.CreateLinkAsync(contentId, new { courseId = scenario.CourseId });

        var studentUpdate = await SendAsync(
            HttpMethod.Put, $"/api/content/{contentId}", scenario.StudentId,
            JsonContent.Create(new { title = "Ändrad", bodyMd = "" }));
        var studentPublish = await SendAsync(
            HttpMethod.Put, $"/api/content/{contentId}/status", scenario.StudentId,
            JsonContent.Create(new { status = "draft" }));

        var teacherUpdate = await SendAsync(
            HttpMethod.Put, $"/api/content/{contentId}", scenario.TeacherId,
            JsonContent.Create(new { title = "Ändrad", bodyMd = "" }));
        var teacherPublish = await SendAsync(
            HttpMethod.Put, $"/api/content/{contentId}/status", scenario.TeacherId,
            JsonContent.Create(new { status = "draft" }));

        Assert.Equal(HttpStatusCode.Forbidden, studentUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, studentPublish.StatusCode);
        Assert.Equal(HttpStatusCode.OK, teacherUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, teacherPublish.StatusCode);
    }

    [Fact]
    public async Task DeleteLink_StudentIsForbidden_TeacherOfTheCourseSucceeds()
    {
        var scenario = await AddScenarioAsync();
        var contentId = await CreateTheoryAsync();
        var linkId = (await _client.CreateLinkAsync(contentId, new { courseId = scenario.CourseId })).Id;

        var byStudent = await SendAsync(
            HttpMethod.Delete, $"/api/content/{contentId}/links/{linkId}", scenario.StudentId);
        var byTeacher = await SendAsync(
            HttpMethod.Delete, $"/api/content/{contentId}/links/{linkId}", scenario.TeacherId);

        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byTeacher.StatusCode);
    }

    [Fact]
    public async Task AddLink_StudentAndOtherTeacherAreForbidden_TeacherOfTheCourseSucceeds()
    {
        var scenario = await AddScenarioAsync();
        var contentId = await CreateTheoryAsync();

        var byStudent = await PostJsonAsync(
            $"/api/content/{contentId}/links", scenario.StudentId, new { courseId = scenario.CourseId });
        var byOtherTeacher = await PostJsonAsync(
            $"/api/content/{contentId}/links", scenario.OtherTeacherId, new { courseId = scenario.CourseId });
        var byTeacher = await PostJsonAsync(
            $"/api/content/{contentId}/links", scenario.TeacherId, new { courseId = scenario.CourseId });

        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byOtherTeacher.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byTeacher.StatusCode);
    }

    [Fact]
    public async Task ReorderModules_StudentIsForbidden_TeacherOfTheCourseSucceeds()
    {
        var scenario = await AddScenarioAsync();
        var first = (await _client.CreateModuleAsync(scenario.CourseId, "Första")).Id;
        var second = (await _client.CreateModuleAsync(scenario.CourseId, "Andra")).Id;
        var order = new { moduleIds = new[] { second, first } };

        var byStudent = await SendAsync(
            HttpMethod.Put, $"/api/courses/{scenario.CourseId}/modules/order", scenario.StudentId,
            JsonContent.Create(order));
        var byTeacher = await SendAsync(
            HttpMethod.Put, $"/api/courses/{scenario.CourseId}/modules/order", scenario.TeacherId,
            JsonContent.Create(order));

        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byTeacher.StatusCode);
    }

    [Fact]
    public async Task UpdateAndDeleteModule_StudentIsForbidden_TeacherOfTheCourseSucceeds()
    {
        var scenario = await AddScenarioAsync();
        var moduleId = (await _client.CreateModuleAsync(scenario.CourseId, "Vecka 1")).Id;
        var update = JsonContent.Create(new { title = "Ändrad" });

        var studentUpdate = await SendAsync(
            HttpMethod.Put, $"/api/courses/{scenario.CourseId}/modules/{moduleId}", scenario.StudentId, update);
        var studentDelete = await SendAsync(
            HttpMethod.Delete, $"/api/courses/{scenario.CourseId}/modules/{moduleId}", scenario.StudentId);

        var teacherUpdate = await SendAsync(
            HttpMethod.Put, $"/api/courses/{scenario.CourseId}/modules/{moduleId}", scenario.TeacherId,
            JsonContent.Create(new { title = "Ändrad" }));
        var teacherDelete = await SendAsync(
            HttpMethod.Delete, $"/api/courses/{scenario.CourseId}/modules/{moduleId}", scenario.TeacherId);

        Assert.Equal(HttpStatusCode.Forbidden, studentUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, studentDelete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, teacherUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, teacherDelete.StatusCode);
    }

    [Fact]
    public async Task MoveLinkToAnotherWeek_StudentIsForbidden_TeacherOfTheCourseSucceeds()
    {
        var scenario = await AddScenarioAsync();
        var firstWeek = (await _client.CreateModuleAsync(scenario.CourseId, "Vecka 1")).Id;
        var secondWeek = (await _client.CreateModuleAsync(scenario.CourseId, "Vecka 2")).Id;
        var contentId = await CreateTheoryAsync();
        var linkId = (await _client.CreateLinkAsync(
            contentId,
            new { courseId = scenario.CourseId, moduleId = firstWeek })).Id;

        var byStudent = await SendAsync(
            HttpMethod.Put, $"/api/content/{contentId}/links/{linkId}/module", scenario.StudentId,
            JsonContent.Create(new { moduleId = secondWeek }));
        var byTeacher = await SendAsync(
            HttpMethod.Put, $"/api/content/{contentId}/links/{linkId}/module", scenario.TeacherId,
            JsonContent.Create(new { moduleId = secondWeek }));

        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byTeacher.StatusCode);
    }

    [Fact]
    public async Task CreateContentWithLink_StudentIsForbidden_TeacherOfTheCourseSucceeds()
    {
        var scenario = await AddScenarioAsync();
        var body = new
        {
            content = new { type = "theory", title = "Ny del", bodyMd = "Text" },
            link = new { courseId = scenario.CourseId }
        };

        var byStudent = await PostJsonAsync("/api/content/with-link", scenario.StudentId, body);
        var byTeacher = await PostJsonAsync("/api/content/with-link", scenario.TeacherId, body);

        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byTeacher.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_StudentIsForbidden_TeacherOfTheContentSucceeds()
    {
        var scenario = await AddScenarioAsync();
        var contentId = await CreateTheoryAsync();
        await _client.CreateLinkAsync(contentId, new { courseId = scenario.CourseId });

        var byStudent = await UploadAsync(contentId, scenario.StudentId);
        var byTeacher = await UploadAsync(contentId, scenario.TeacherId);

        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byTeacher.StatusCode);
    }

    [Fact]
    public async Task Grade_StudentOtherTeacherAndAdminAreForbidden_TeacherOfTheLinkSucceeds()
    {
        var scenario = await AddScenarioAsync();
        var assignmentId = await CreateOnSiteAssignmentAsync();
        var linkId = (await _client.CreateLinkAsync(assignmentId, new { courseId = scenario.CourseId })).Id;
        Guid submissionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            submissionId = (await db.Submissions.SingleAsync(
                submission => submission.ContentLinkId == linkId)).Id;
        }

        var body = new { level = "pass", feedback = "Bra" };
        var byStudent = await PostJsonAsync($"/api/submissions/{submissionId}/grade", scenario.StudentId, body);
        var byOtherTeacher = await PostJsonAsync(
            $"/api/submissions/{submissionId}/grade", scenario.OtherTeacherId, body);
        var byAdmin = await PostJsonAsync($"/api/submissions/{submissionId}/grade", scenario.AdminId, body);
        var byTeacher = await PostJsonAsync($"/api/submissions/{submissionId}/grade", scenario.TeacherId, body);

        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byOtherTeacher.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byTeacher.StatusCode);
    }

    [Fact]
    public async Task SchoolUserGroupEnrollmentAndCourseLinks_RequireAdmin()
    {
        var scenario = await AddScenarioAsync();
        var student = scenario.StudentId;

        var school = await PostJsonAsync("/api/schools", student, new { name = "Ny skola" });
        var user = await PostJsonAsync("/api/users", student, new
        {
            schoolId = scenario.SchoolId,
            name = "Ny Elev",
            email = $"{Guid.NewGuid():N}@example.com",
            role = "student"
        });
        var group = await PostJsonAsync("/api/groups", student, new
        {
            schoolId = scenario.SchoolId,
            type = "class",
            name = "NET26"
        });
        var courseLink = await SendAsync(
            HttpMethod.Post, $"/api/groups/{scenario.GroupId}/courses/{scenario.OtherCourseId}", student);
        var member = await PostJsonAsync(
            $"/api/groups/{scenario.GroupId}/members", student, new { userId = scenario.StudentId });

        var adminSchool = await PostJsonAsync("/api/schools", Guid.Empty, new { name = "Admins skola" });

        Assert.Equal(HttpStatusCode.Forbidden, school.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, user.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, group.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, courseLink.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);
        Assert.Equal(HttpStatusCode.Created, adminSchool.StatusCode);
    }

    [Fact]
    public async Task CreateCourse_StudentAndTeacherAreForbidden_AdminSucceeds()
    {
        var scenario = await AddScenarioAsync();
        // Admin är placeholder-användaren, så kursen skapas i dess skola.
        var code = $"NY{Guid.NewGuid():N}"[..10];
        var course = new { schoolId = PlaceholderIds.SchoolId, title = "Ny kurs", code };

        var byStudent = await PostJsonAsync("/api/courses", scenario.StudentId, course);
        var byTeacher = await PostJsonAsync("/api/courses", scenario.TeacherId, course);
        var byAdmin = await PostJsonAsync("/api/courses", Guid.Empty, course);

        Assert.Equal(HttpStatusCode.Forbidden, byStudent.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byTeacher.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byAdmin.StatusCode);
        Assert.Equal("application/problem+json", byStudent.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Du har inte behörighet", await byStudent.Content.ReadAsStringAsync());
    }

    // Guid.Empty betyder placeholder-användaren, som är Admin.
    private Task<HttpResponseMessage> PostJsonAsync(string url, Guid asUser, object body) =>
        SendAsync(HttpMethod.Post, url, asUser, JsonContent.Create(body));

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, Guid asUser, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (asUser != Guid.Empty)
        {
            request.Headers.Add(TestAuthHandler.Header, asUser.ToString());
        }

        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> UploadAsync(Guid contentId, Guid asUser)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([1, 2, 3]), "file", "slides.pdf");
        return await SendAsync(HttpMethod.Post, $"/api/content/{contentId}/attachments", asUser, form);
    }

    private async Task<Guid> CreateTheoryAsync()
    {
        return (await _client.PostJsonAsync<IdResponse>(
            "/api/content", new { type = "theory", title = "Teori", bodyMd = "Text" })).Id;
    }

    private async Task<Guid> CreateOnSiteAssignmentAsync()
    {
        return (await _client.PostJsonAsync<IdResponse>("/api/content", new
        {
            type = "assignment",
            title = "Redovisning",
            bodyMd = "",
            gradeScale = "passFail",
            submissionModes = new[] { "onSite" }
        })).Id;
    }

    private async Task<Scenario> AddScenarioAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        // Admin ser bara sin egen skola, så scenariot ligger i den. Placeholder är lärare i kurserna, för att kunna
        // förbereda innehållet. AdminId är en admin som inte undervisar, AdminTeacherId en som står som lärare.
        var school = await db.Schools.SingleAsync(found => found.Id == PlaceholderIds.SchoolId);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var otherGroup = await TestData.AddGroupAsync(db, school.Id);
        var course = await TestData.AddCourseAsync(db, school.Id, group.Id);
        var otherCourse = await TestData.AddCourseAsync(db, school.Id, otherGroup.Id);
        var admin = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
        var adminTeacher = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
        await TestData.AddCourseTeacherAsync(db, course, adminTeacher);

        var student = await TestData.AddMemberAsync(db, group);
        var teacher = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);
        var otherTeacher = await TestData.AddMemberAsync(
            db, otherGroup, GroupRole.Teacher, UserRole.Teacher);
        await TestData.AddCourseTeacherAsync(db, course, teacher);
        await TestData.AddCourseTeacherAsync(db, otherCourse, otherTeacher);

        return new Scenario(
            school.Id, group.Id, course.Id, otherCourse.Id, student.Id, teacher.Id, otherTeacher.Id, admin.Id,
            adminTeacher.Id);
    }
}
