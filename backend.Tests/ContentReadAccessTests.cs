using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Vem som får läsa och ändra ett innehåll via GET/PUT /api/content/{id}: lärare på en länk, elev
// där en länk syns för eleverna, och admin i författarens skola. Alla andra får 404 vid läsning.
public class ContentReadAccessTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _placeholder = factory.CreateClient();

    private record IdResponse(Guid Id);

    private record CreatedWithLink(IdResponse Content, IdResponse Link);

    private record Scene(Guid SchoolId, Guid GroupId, Guid CourseId, Guid ModuleId, Guid ContentId);

    private async Task<Scene> AddSceneAsync(string? moduleOpensAt = null, string? linkPublishAt = null)
    {
        Guid schoolId;
        Guid groupId;
        Guid courseId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var school = await TestData.AddSchoolAsync(db);
            var group = await TestData.AddGroupAsync(db, school.Id);
            var course = await TestData.AddCourseAsync(db, school.Id, group.Id);
            (schoolId, groupId, courseId) = (school.Id, group.Id, course.Id);
        }

        var module = await _placeholder.PostAsJsonAsync(
            $"/api/courses/{courseId}/modules", new { title = "Vecka 1", publishAt = moduleOpensAt });
        module.EnsureSuccessStatusCode();
        var moduleId = (await module.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var created = await _placeholder.PostAsJsonAsync("/api/content/with-link", new
        {
            content = new { type = "theory", title = "Hemlig teori", bodyMd = "Bara för kursen" },
            link = new { courseId, moduleId, publishAt = linkPublishAt }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var result = (await created.Content.ReadFromJsonAsync<CreatedWithLink>())!;
        return new Scene(schoolId, groupId, courseId, moduleId, result.Content.Id);
    }

    private async Task<HttpClient> ClientForMemberAsync(
        Guid groupId, GroupRole role = GroupRole.Student, Guid? otherSchoolId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = otherSchoolId is null
            ? (await db.Groups.FindAsync(groupId))!
            : await TestData.AddGroupAsync(db, otherSchoolId);
        var user = await TestData.AddMemberAsync(db, group, role);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, user.Id.ToString());
        return client;
    }

    private async Task<HttpClient> ClientForNewSchoolStudentAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var user = await TestData.AddMemberAsync(db, group);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, user.Id.ToString());
        return client;
    }

    [Fact]
    public async Task Student_InTheCourse_CanReadPublishedContent()
    {
        var scene = await AddSceneAsync();
        using var student = await ClientForMemberAsync(scene.GroupId);

        var response = await student.GetAsync($"/api/content/{scene.ContentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Student_OfAnotherCourse_GetsNotFound()
    {
        var scene = await AddSceneAsync();
        var other = await AddSceneAsync();
        using var student = await ClientForMemberAsync(other.GroupId);

        var response = await student.GetAsync($"/api/content/{scene.ContentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Student_InAnotherSchool_GetsNotFound()
    {
        var scene = await AddSceneAsync();
        using var student = await ClientForNewSchoolStudentAsync();

        var response = await student.GetAsync($"/api/content/{scene.ContentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Student_CannotReadContentInALockedWeek()
    {
        var scene = await AddSceneAsync(moduleOpensAt: "2099-01-05T08:00:00Z");
        using var student = await ClientForMemberAsync(scene.GroupId);

        var response = await student.GetAsync($"/api/content/{scene.ContentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Student_CannotReadContentBeforeTheLinkIsPublished()
    {
        var scene = await AddSceneAsync(linkPublishAt: "2099-01-05T08:00:00Z");
        using var student = await ClientForMemberAsync(scene.GroupId);

        var response = await student.GetAsync($"/api/content/{scene.ContentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Teacher_OfTheCourse_CanReadAndEdit()
    {
        var scene = await AddSceneAsync();

        var read = await _placeholder.GetAsync($"/api/content/{scene.ContentId}");
        var edit = await _placeholder.PutAsJsonAsync(
            $"/api/content/{scene.ContentId}", new { title = "Ny titel", bodyMd = "Ny text" });

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
    }

    [Fact]
    public async Task Author_RemovedFromTheCourse_CanNoLongerEditOrRead()
    {
        Guid courseId;
        Guid teacherId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var school = await TestData.AddSchoolAsync(db);
            var group = await TestData.AddGroupAsync(db, school.Id);
            var course = await TestData.AddCourseAsync(db, school.Id, group.Id, placeholderTeaches: false);
            var teacher = await TestData.AddCourseTeacherAsync(db, course);
            (courseId, teacherId) = (course.Id, teacher.Id);
        }

        using var teacherClient = factory.CreateClient();
        teacherClient.DefaultRequestHeaders.Add(TestAuthHandler.Header, teacherId.ToString());
        var created = await teacherClient.PostAsJsonAsync("/api/content/with-link", new
        {
            content = new { type = "theory", title = "Min del", bodyMd = "Text" },
            link = new { courseId }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var contentId = (await created.Content.ReadFromJsonAsync<CreatedWithLink>())!.Content.Id;
        Assert.Equal(
            HttpStatusCode.OK,
            (await teacherClient.PutAsJsonAsync($"/api/content/{contentId}", new { title = "Ok", bodyMd = "" }))
            .StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.CourseTeachers
                .Where(courseTeacher => courseTeacher.UserId == teacherId)
                .ExecuteDeleteAsync();
        }

        var edit = await teacherClient.PutAsJsonAsync($"/api/content/{contentId}", new { title = "Nej", bodyMd = "" });
        var status = await teacherClient.PutAsJsonAsync(
            $"/api/content/{contentId}/status", new { status = "draft" });
        var read = await teacherClient.GetAsync($"/api/content/{contentId}");

        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task Teacher_CreatingANewPart_StillWorks()
    {
        Guid courseId;
        Guid teacherId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var school = await TestData.AddSchoolAsync(db);
            var group = await TestData.AddGroupAsync(db, school.Id);
            var course = await TestData.AddCourseAsync(db, school.Id, group.Id, placeholderTeaches: false);
            var teacher = await TestData.AddCourseTeacherAsync(db, course);
            (courseId, teacherId) = (course.Id, teacher.Id);
        }

        using var teacherClient = factory.CreateClient();
        teacherClient.DefaultRequestHeaders.Add(TestAuthHandler.Header, teacherId.ToString());
        var week = await teacherClient.PostAsJsonAsync($"/api/courses/{courseId}/modules", new { title = "Vecka 1" });
        week.EnsureSuccessStatusCode();
        var moduleId = (await week.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var created = await teacherClient.PostAsJsonAsync("/api/content/with-link", new
        {
            content = new
            {
                type = "assignment",
                title = "Ny uppgift",
                bodyMd = "",
                submissionModes = new[] { "text" }
            },
            link = new { courseId, moduleId }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var contentId = (await created.Content.ReadFromJsonAsync<CreatedWithLink>())!.Content.Id;

        var edit = await teacherClient.PutAsJsonAsync(
            $"/api/content/{contentId}",
            new { title = "Ny uppgift 2", bodyMd = "", submissionModes = new[] { "text" } });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
    }

    [Fact]
    public async Task Comments_AndAttachments_FollowTheSameReadRule()
    {
        var scene = await AddSceneAsync();
        using var outsider = await ClientForNewSchoolStudentAsync();

        var comment = await outsider.PostAsJsonAsync(
            $"/api/content/{scene.ContentId}/comments", new { bodyMd = "Smyg" });
        var attachments = await outsider.GetAsync($"/api/content/{scene.ContentId}/attachments");

        Assert.Equal(HttpStatusCode.NotFound, comment.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, attachments.StatusCode);
    }
}
