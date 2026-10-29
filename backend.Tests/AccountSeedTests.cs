using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

// Inloggningskontona, som seeden lägger in vid varje start. Fabriken startar appen, så seeden har körts en gång
// när testerna börjar. JWT-fabriken används så att /api/me svarar för den som loggar in.
public class AccountSeedTests(JwtTestWebApplicationFactory factory) : IClassFixture<JwtTestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record LoginResponse(string AccessToken);

    private record MeResponse(Guid Id, string Email, string Role, List<CourseRoleResponse> Courses);

    private record CourseRoleResponse(Guid CourseId, string Role);

    [Theory]
    [InlineData("admin", "admin")]
    [InlineData("larare", "larare")]
    [InlineData("elev", "elev")]
    public async Task Account_CanSignIn(string username, string password)
    {
        var response = await LoginAsync(username, password);

        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(login!.AccessToken));
    }

    [Fact]
    public async Task Accounts_HaveTheRightRoles_AndTheTeacherTeachesTheDemoCourse()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var demoCourse = await db.Courses.SingleAsync(course => course.Code == AccountSeed.CourseCode);

        var teacher = await MeAsync("larare", "larare");
        var admin = await MeAsync("admin", "admin");
        var student = await MeAsync("elev", "elev");

        Assert.Equal(("Teacher", "larare"), (teacher.Role, teacher.Email));
        Assert.Equal("Admin", admin.Role);
        Assert.Equal("Student", student.Role);
        Assert.Contains(teacher.Courses, course => course.CourseId == demoCourse.Id && course.Role == "Teacher");
        Assert.Contains(student.Courses, course => course.CourseId == demoCourse.Id && course.Role == "Student");
    }

    [Fact]
    public async Task AccountSeed_RunTwice_CreatesNothingNew()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var before = await CountDemoRowsAsync(db);

        await scope.ServiceProvider.GetRequiredService<AccountSeed>().SeedAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<AccountSeed>().SeedAsync(CancellationToken.None);

        var after = await CountDemoRowsAsync(db);
        Assert.Equal(before, after);
        Assert.Equal(3, after.Users);
        Assert.Equal(1, after.Groups);
        Assert.Equal(1, after.Courses);
        Assert.Equal(1, after.Modules);
        Assert.Equal(3, after.Content);
        Assert.Equal(2, after.Enrollments);
        Assert.Equal(1, after.GroupCourses);
    }

    [Fact]
    public async Task AccountSeed_ResetsThePassword_AndNeverTouchesOtherUsers()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var elev = await db.Users.SingleAsync(user => user.Email == "elev");
        var otherUser = await TestData.AddUserAsync(db);
        var otherHashBefore = otherUser.PasswordHash;

        elev.PasswordHash = "ogiltig";
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<AccountSeed>().SeedAsync(CancellationToken.None);

        var response = await LoginAsync("elev", "elev");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(otherHashBefore, (await db.Users.SingleAsync(user => user.Id == otherUser.Id)).PasswordHash);
    }

    [Fact]
    public async Task AccountSeed_LinksTheDemoClassOnlyToTheDemoCourse()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var demoGroup = await db.Groups.SingleAsync(group => group.Name == AccountSeed.ClassName);
        var otherCourse = await TestData.AddCourseAsync(db, demoGroup.SchoolId);

        await scope.ServiceProvider.GetRequiredService<AccountSeed>().SeedAsync(CancellationToken.None);

        var linkedCodes = await db.GroupCourses
            .Where(link => link.GroupId == demoGroup.Id)
            .Join(db.Courses, link => link.CourseId, course => course.Id, (_, course) => course.Code)
            .ToListAsync();
        Assert.Equal([AccountSeed.CourseCode], linkedCodes);
        Assert.False(await db.GroupCourses.AnyAsync(link => link.CourseId == otherCourse.Id));
    }

    private static async Task<CountSnapshot> CountDemoRowsAsync(AppDb db)
    {
        var demoEmails = new[] { "admin", "larare", "elev" };
        var users = await db.Users.CountAsync(user => demoEmails.Contains(user.Email));
        var groups = await db.Groups.CountAsync(group => group.Name == AccountSeed.ClassName);
        var courses = await db.Courses.CountAsync(course => course.Code == AccountSeed.CourseCode);
        var modules = await db.Modules.CountAsync(module => module.Title == "Vecka 1");
        var content = await db.Content.CountAsync(item => item.Title.StartsWith("Välkommen")
            || item.Title.StartsWith("Så fungerar")
            || item.Title.StartsWith("Skriv en kort"));
        var enrollments = await db.Enrollments.CountAsync(enrollment => db.Groups.Any(
            group => group.Id == enrollment.GroupId && group.Name == AccountSeed.ClassName));
        var groupCourses = await db.GroupCourses.CountAsync(link => db.Groups.Any(
            group => group.Id == link.GroupId && group.Name == AccountSeed.ClassName));
        return new CountSnapshot(users, groups, courses, modules, content, enrollments, groupCourses);
    }

    private async Task<MeResponse> MeAsync(string username, string password)
    {
        var login = await LoginAsync(username, password);
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MeResponse>())!;
    }

    private Task<HttpResponseMessage> LoginAsync(string username, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { username, password });

    private sealed record CountSnapshot(
        int Users, int Groups, int Courses, int Modules, int Content, int Enrollments, int GroupCourses);
}
