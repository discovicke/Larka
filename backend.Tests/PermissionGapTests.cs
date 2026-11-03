using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Luckor i behörigheterna (#360): vem som kan vara lärare i en klass, e-postregeln och vem som ser kursens personal.
public class PermissionGapTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record MemberResponse(Guid UserId, string Name, string Role);

    private record ImportLine(string Email, string Outcome);

    private record ImportResponse(List<ImportLine> Lines);

    private record StaffResponse(Guid UserId, string Name, string Email, List<string> Groups);

    [Theory]
    [InlineData(UserRole.Admin, "teacher")]
    [InlineData(UserRole.Student, "teacher")]
    public async Task AddMember_AsClassTeacher_RejectsAdminAndStudent(UserRole userRole, string role)
    {
        await using var db = OpenDb();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var user = await TestData.AddUserAsync(db, group.SchoolId, userRole);

        var response = await _client.PostAsJsonAsync(
            $"/api/groups/{group.Id}/members", new { userId = user.Id, role });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("only a teacher", await response.Content.ReadAsStringAsync());
        Assert.False(await db.Enrollments.AnyAsync(row => row.GroupId == group.Id && row.UserId == user.Id));
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Student)]
    public async Task UpdateMember_ToClassTeacher_RejectsAdminAndStudent(UserRole userRole)
    {
        await using var db = OpenDb();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var member = await TestData.AddMemberAsync(db, group, GroupRole.Student, userRole);

        var asTeacher = await _client.PutAsJsonAsync(
            $"/api/groups/{group.Id}/members/{member.Id}", new { role = "teacher" });

        Assert.Equal(HttpStatusCode.BadRequest, asTeacher.StatusCode);
    }

    [Fact]
    public async Task AddCourseTeacher_RejectsStudent()
    {
        await using var db = OpenDb();
        var course = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, placeholderTeaches: false);
        var student = await TestData.AddUserAsync(db, PlaceholderIds.SchoolId);

        var response = await _client.PostAsJsonAsync($"/api/courses/{course.Id}/teachers", new { userId = student.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Mentor")]
    [InlineData("mentor")]
    public async Task AddMember_WithTheRemovedMentorRole_ReturnsBadRequest(string role)
    {
        await using var db = OpenDb();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var teacher = await TestData.AddUserAsync(db, group.SchoolId, UserRole.Teacher);

        var response = await _client.PostAsJsonAsync(
            $"/api/groups/{group.Id}/members", new { userId = teacher.Id, role });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await db.Enrollments.AnyAsync(enrollment => enrollment.GroupId == group.Id));
    }

    [Fact]
    public async Task UpdateMember_ToTheRemovedMentorRole_ReturnsBadRequest_AndKeepsTheRole()
    {
        await using var db = OpenDb();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var teacher = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);

        var response = await _client.PutAsJsonAsync(
            $"/api/groups/{group.Id}/members/{teacher.Id}", new { role = "Mentor" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var members = await _client.GetFromJsonAsync<List<MemberResponse>>($"/api/groups/{group.Id}/members");
        Assert.Equal("Teacher", Assert.Single(members!).Role);
    }

    [Theory]
    [InlineData("inte-en-adress")]
    [InlineData("anna@skola")]
    [InlineData("anna @skola.se")]
    [InlineData("@skola.se")]
    public async Task CreateUser_WithInvalidEmail_ReturnsBadRequest(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/users", new { schoolId = PlaceholderIds.SchoolId, name = "Anna", email, role = "student" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("email is not a valid address", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ImportStudents_UsesTheSameEmailRule_AsTheClientPreview()
    {
        await using var db = OpenDb();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var valid = $"ok-{Guid.NewGuid():N}@skola.se";

        var response = await _client.PostAsJsonAsync($"/api/groups/{group.Id}/members/import", new
        {
            lines = new[]
            {
                new { name = "Utan punkt", email = "elev@skola" },
                new { name = "Giltig", email = valid },
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<ImportResponse>())!;
        Assert.Equal(["Invalid", "Created"], result.Lines.Select(line => line.Outcome));
        Assert.False(await db.Users.AnyAsync(user => user.Email == "elev@skola"));
    }

    [Fact]
    public async Task GetStaff_IsForTheCourseTeachersStudentsAndAdminOnly()
    {
        await using var db = OpenDb();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var course = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, group.Id, placeholderTeaches: false);
        var teacher = await TestData.AddCourseTeacherAsync(db, course);
        var student = await TestData.AddMemberAsync(db, group);
        var otherGroup = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var otherStudent = await TestData.AddMemberAsync(db, otherGroup);
        var otherTeacher = await TestData.AddUserAsync(db, PlaceholderIds.SchoolId, UserRole.Teacher);
        var otherSchoolAdmin = await TestData.AddUserAsync(db, role: UserRole.Admin);
        var path = $"/api/courses/{course.Id}/staff";

        foreach (var allowed in new[] { teacher.Id, student.Id, PlaceholderIds.UserId })
        {
            var response = await GetAsync(path, allowed);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var staff = await response.Content.ReadFromJsonAsync<List<StaffResponse>>();
            Assert.Equal(teacher.Id, Assert.Single(staff!).UserId);
        }

        foreach (var denied in new[] { otherStudent.Id, otherTeacher.Id, otherSchoolAdmin.Id })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(path, denied)).StatusCode);
        }
    }

    private AppDb OpenDb() => factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDb>();

    private async Task<HttpResponseMessage> GetAsync(string path, Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }
}
