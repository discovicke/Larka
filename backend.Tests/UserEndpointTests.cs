using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class UserEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record UserResponse(
        Guid Id, Guid SchoolId, string Name, string Email, string Role, DateTimeOffset? DeactivatedAt);

    [Fact]
    public async Task CreateUser_ReturnsCreated_WithLowercaseEmail()
    {
        var schoolId = PlaceholderIds.SchoolId;
        var email = $"Maria.{Guid.NewGuid():N}@Example.com";

        var response = await _client.PostAsJsonAsync(
            "/api/users", new { schoolId, name = "Maria Ek", email, role = "teacher" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(email.ToLowerInvariant(), user!.Email);
        Assert.Equal("Teacher", user.Role);

        var found = await _client.GetFromJsonAsync<UserResponse>($"/api/users/{user.Id}");
        var all = await _client.GetFromJsonAsync<List<UserResponse>>("/api/users");
        Assert.Equal("Maria Ek", found!.Name);
        Assert.Contains(all!, item => item.Id == user.Id);
    }

    [Fact]
    public async Task CreateUser_WithEmailInUse_ReturnsBadRequest()
    {
        var schoolId = await AddSchoolAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/users", new { schoolId, name = "Första", email, role = "student" });

        var response = await _client.PostAsJsonAsync(
            "/api/users", new { schoolId, name = "Andra", email = email.ToUpperInvariant(), role = "student" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("email is already in use", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("inte-en-adress")]
    [InlineData("")]
    public async Task CreateUser_WithInvalidEmail_ReturnsBadRequest(string email)
    {
        var schoolId = await AddSchoolAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/users", new { schoolId, name = "Test", email, role = "student" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("email is not a valid address", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateUser_WithUnknownRoleOrSchool_ReturnsBadRequest()
    {
        var schoolId = await AddSchoolAsync();

        var unknownRole = await _client.PostAsJsonAsync("/api/users",
            new { schoolId, name = "Test", email = $"{Guid.NewGuid():N}@example.com", role = "rektor" });
        var email = $"{Guid.NewGuid():N}@example.com";
        var unknownSchool = await _client.PostAsJsonAsync(
            "/api/users", new { schoolId = Guid.NewGuid(), name = "Test", email, role = "student" });

        Assert.Equal(HttpStatusCode.BadRequest, unknownRole.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownSchool.StatusCode);
        Assert.Contains("school does not exist", await unknownSchool.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateUser_WithPassword_CanSignIn()
    {
        var schoolId = await AddSchoolAsync();
        var email = $"{Guid.NewGuid():N}@example.com";

        var created = await _client.PostAsJsonAsync(
            "/api/users", new { schoolId, name = "Ny Elev", email, role = "student", password = "hemligt123" });
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { username = email, password = "hemligt123" });
        var wrong = await _client.PostAsJsonAsync("/api/auth/login", new { username = email, password = "fel-losen1" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.DoesNotContain("hemligt123", await created.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithoutPassword_CannotSignIn()
    {
        var schoolId = await AddSchoolAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/users", new { schoolId, name = "Ny Elev", email, role = "student" });

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { username = email, password = "hemligt123" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithTooShortPassword_ReturnsBadRequest()
    {
        var schoolId = await AddSchoolAsync();
        var email = $"{Guid.NewGuid():N}@example.com";

        var response = await _client.PostAsJsonAsync(
            "/api/users", new { schoolId, name = "Ny Elev", email, role = "student", password = "kort" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("password must be at least 8 characters", await response.Content.ReadAsStringAsync());
        Assert.Null((await _client.GetFromJsonAsync<List<UserResponse>>("/api/users"))!
            .FirstOrDefault(user => user.Email == email));
    }

    [Fact]
    public async Task GetUser_ThatDoesNotExist_Returns404()
    {
        var response = await _client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_AsAdmin_ReturnsOnlyTheAdminsSchool()
    {
        var own = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);
        var other = await AddUserAsync(null, UserRole.Student);

        var all = await _client.GetFromJsonAsync<List<UserResponse>>("/api/users");

        Assert.Contains(all!, item => item.Id == own.Id && item.Email == own.Email && item.Role == "Student");
        Assert.DoesNotContain(all!, item => item.Id == other.Id);
        Assert.All(all!, item => Assert.Equal(PlaceholderIds.SchoolId, item.SchoolId));
    }

    [Fact]
    public async Task ListUsers_AsStudentTeacherOrMentor_IsForbiddenAndLeaksNothing()
    {
        var student = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);
        var teacher = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Teacher);
        var mentor = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Teacher, GroupRole.Mentor);

        foreach (var caller in new[] { student, teacher, mentor })
        {
            var response = await GetAsync("/api/users", caller.Id);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.DoesNotContain(student.Email, await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task ListUsers_AsAdminOfAnotherSchool_ReturnsThatSchoolOnly()
    {
        var otherSchool = await AddSchoolAsync();
        var otherAdmin = await AddUserAsync(otherSchool, UserRole.Admin);
        var own = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);

        var response = await GetAsync("/api/users", otherAdmin.Id);

        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([otherAdmin.Id], users!.Select(item => item.Id));
        Assert.DoesNotContain(users!, item => item.Id == own.Id);
    }

    [Fact]
    public async Task GetUser_AsAdmin_ReturnsUsersInTheSameSchoolOnly()
    {
        var own = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);
        var other = await AddUserAsync(null, UserRole.Student);

        var found = await _client.GetAsync($"/api/users/{own.Id}");
        var foreign = await _client.GetAsync($"/api/users/{other.Id}");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(own.Email, (await found.Content.ReadFromJsonAsync<UserResponse>())!.Email);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task GetUser_AsStudentTeacherMentorOrOtherSchool_IsForbidden()
    {
        var target = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);
        var classmate = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);
        var teacher = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Teacher);
        var mentor = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Teacher, GroupRole.Mentor);
        var outsider = await AddUserAsync(null, UserRole.Student);

        foreach (var caller in new[] { target, classmate, teacher, mentor, outsider })
        {
            var response = await GetAsync($"/api/users/{target.Id}", caller.Id);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain(target.Email, await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task UpdateRole_AsAdmin_PromotesAStudentToTeacher()
    {
        var student = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);

        var response = await _client.PutAsJsonAsync($"/api/users/{student.Id}/role", new { role = "teacher" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Teacher", (await response.Content.ReadFromJsonAsync<UserResponse>())!.Role);
        var found = await _client.GetFromJsonAsync<UserResponse>($"/api/users/{student.Id}");
        Assert.Equal("Teacher", found!.Role);
    }

    [Fact]
    public async Task UpdateRole_ToStudent_RemovesTheUserAsCourseTeacher()
    {
        var teacher = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Teacher);
        Guid courseId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var course = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId);
            await TestData.AddCourseTeacherAsync(db, course, teacher);
            courseId = course.Id;
        }

        var response = await _client.PutAsJsonAsync($"/api/users/{teacher.Id}/role", new { role = "student" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppDb>();
        Assert.False(checkDb.CourseTeachers.Any(row => row.UserId == teacher.Id));
        Assert.True(checkDb.CourseTeachers.Any(row =>
            row.CourseId == courseId && row.UserId == PlaceholderIds.UserId));
    }

    [Fact]
    public async Task UpdateRole_ToAdmin_KeepsCourseTeaching()
    {
        var teacher = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Teacher);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var course = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId);
            await TestData.AddCourseTeacherAsync(db, course, teacher);
        }

        var response = await _client.PutAsJsonAsync($"/api/users/{teacher.Id}/role", new { role = "admin" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppDb>();
        Assert.True(checkDb.CourseTeachers.Any(row => row.UserId == teacher.Id));
    }

    [Fact]
    public async Task UpdateRole_OfYourself_ReturnsConflict()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/users/{PlaceholderIds.UserId}/role", new { role = "teacher" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("you cannot change your own role", await response.Content.ReadAsStringAsync());
        var self = await _client.GetFromJsonAsync<UserResponse>($"/api/users/{PlaceholderIds.UserId}");
        Assert.Equal("Admin", self!.Role);
    }

    [Fact]
    public async Task UpdateRole_ForAnotherSchoolOrUnknownUser_Returns404()
    {
        var other = await AddUserAsync(null, UserRole.Student);

        var foreign = await _client.PutAsJsonAsync($"/api/users/{other.Id}/role", new { role = "teacher" });
        var unknown = await _client.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}/role", new { role = "teacher" });

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_WithUnknownRole_ReturnsBadRequest()
    {
        var student = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);

        var response = await _client.PutAsJsonAsync($"/api/users/{student.Id}/role", new { role = "rektor" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_AsTeacherOrStudent_IsForbidden()
    {
        var target = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);
        var teacher = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Teacher);
        var student = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);

        foreach (var caller in new[] { teacher, student })
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/api/users/{target.Id}/role")
            {
                Content = JsonContent.Create(new { role = "admin" })
            };
            request.Headers.Add(TestAuthHandler.Header, caller.Id.ToString());
            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.Equal("Student", (await _client.GetFromJsonAsync<UserResponse>($"/api/users/{target.Id}"))!.Role);
    }

    [Fact]
    public async Task Deactivate_BlocksSignIn_AndReactivateLetsThemBackIn()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var created = await _client.PostAsJsonAsync("/api/users", new
        {
            schoolId = PlaceholderIds.SchoolId,
            name = "Spärr Elev",
            email,
            role = "student",
            password = "hemligt123"
        });
        var user = await created.Content.ReadFromJsonAsync<UserResponse>();

        var deactivated = await _client.PostAsync($"/api/users/{user!.Id}/deactivate", null);
        var blocked = await _client.PostAsJsonAsync(
            "/api/auth/login", new { username = email, password = "hemligt123" });
        var wrongPassword = await _client.PostAsJsonAsync(
            "/api/auth/login", new { username = email, password = "fel-losen1" });

        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.NotNull((await deactivated.Content.ReadFromJsonAsync<UserResponse>())!.DeactivatedAt);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains("Ditt konto är spärrat.", await blocked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        var reactivated = await _client.PostAsync($"/api/users/{user.Id}/reactivate", null);
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { username = email, password = "hemligt123" });

        Assert.Null((await reactivated.Content.ReadFromJsonAsync<UserResponse>())!.DeactivatedAt);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Deactivate_HidesTheUserFromTheClassList_ButKeepsTheEnrollment()
    {
        Guid groupId;
        Backend.Domain.Entities.User student;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
            student = await TestData.AddMemberAsync(db, group);
            groupId = group.Id;
        }

        await _client.PostAsync($"/api/users/{student.Id}/deactivate", null);
        var members = await _client.GetStringAsync($"/api/groups/{groupId}/members");

        Assert.DoesNotContain(student.Id.ToString(), members);
        using var check = factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppDb>();
        Assert.True(checkDb.Enrollments.Any(row => row.UserId == student.Id && row.GroupId == groupId));
    }

    [Fact]
    public async Task Deactivate_Yourself_ReturnsConflict()
    {
        var response = await _client.PostAsync($"/api/users/{PlaceholderIds.UserId}/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("you cannot deactivate yourself", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Deactivate_InAnotherSchool_Returns404_AndAsNonAdmin_Returns403()
    {
        var other = await AddUserAsync(null, UserRole.Student);
        var target = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Student);
        var teacher = await AddUserAsync(PlaceholderIds.SchoolId, UserRole.Teacher);

        var foreign = await _client.PostAsync($"/api/users/{other.Id}/deactivate", null);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{target.Id}/deactivate");
        request.Headers.Add(TestAuthHandler.Header, teacher.Id.ToString());
        var forbidden = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Null((await _client.GetFromJsonAsync<UserResponse>($"/api/users/{target.Id}"))!.DeactivatedAt);
    }

    // En användare i skolan (eller en ny skola om schoolId saknas). Med groupRole läggs hen också i en grupp.
    private async Task<Backend.Domain.Entities.User> AddUserAsync(
        Guid? schoolId, UserRole role, GroupRole? groupRole = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        if (groupRole is null)
        {
            return await TestData.AddUserAsync(db, schoolId, role);
        }

        var group = await TestData.AddGroupAsync(db, schoolId);
        return await TestData.AddMemberAsync(db, group, groupRole.Value, role);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<Guid> AddSchoolAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        return (await TestData.AddSchoolAsync(db)).Id;
    }
}
