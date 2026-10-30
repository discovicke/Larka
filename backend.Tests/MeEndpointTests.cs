using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class MeEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record MeResponse(
        Guid Id, string Name, string Email, string Role, SchoolResponse School, List<CourseRoleResponse> Courses,
        List<GroupResponse> Groups);

    private record SchoolResponse(Guid Id, string Name);

    private record GroupResponse(Guid Id, string Name);

    private record NotificationSettingsResponse(List<string> Muted);

    private record CourseRoleResponse(Guid CourseId, string Role);

    [Fact]
    public async Task Me_ForAdmin_HasNoCourseRoleOnTheSchoolsCourses()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var admin = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
        await TestData.AddCourseAsync(db, school.Id, placeholderTeaches: false);
        await TestData.AddCourseAsync(db, school.Id, placeholderTeaches: false);

        var me = await GetMeAsync(admin.Id);

        Assert.Equal(("Admin", admin.Name, admin.Email), (me!.Role, me.Name, me.Email));
        Assert.Equal((school.Id, school.Name), (me.School.Id, me.School.Name));
        Assert.Empty(me.Courses);
    }

    [Fact]
    public async Task Me_ForAdminWhoAlsoTeaches_IsTeacherOnlyInThatCourse()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var admin = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
        var taught = await TestData.AddCourseAsync(db, school.Id, placeholderTeaches: false);
        await TestData.AddCourseAsync(db, school.Id, placeholderTeaches: false);
        await TestData.AddCourseTeacherAsync(db, taught, admin);

        var me = await GetMeAsync(admin.Id);

        var course = Assert.Single(me!.Courses);
        Assert.Equal((taught.Id, "Teacher"), (course.CourseId, course.Role));
    }

    [Fact]
    public async Task Me_ForTeacher_PicksTheStrongestRolePerCourse()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var otherGroup = await TestData.AddGroupAsync(db, school.Id);
        var course = await TestData.AddCourseAsync(db, school.Id, group.Id);
        var teacher = await TestData.AddUserAsync(db, school.Id, UserRole.Teacher);
        db.CourseTeachers.Add(new CourseTeacher { CourseId = course.Id, UserId = teacher.Id });
        db.Enrollments.AddRange(
            new Enrollment { UserId = teacher.Id, GroupId = group.Id, Role = GroupRole.Mentor },
            // Samma kurs via en annan grupp, där läraren bara är elev: Teacher, från kursen, vinner.
            new Enrollment { UserId = teacher.Id, GroupId = otherGroup.Id, Role = GroupRole.Student });
        db.GroupCourses.Add(new GroupCourse { GroupId = otherGroup.Id, CourseId = course.Id });
        await db.SaveChangesAsync();

        var me = await GetMeAsync(teacher.Id);

        Assert.Equal("Teacher", me!.Role);
        var courseRole = Assert.Single(me.Courses);
        Assert.Equal((course.Id, "Teacher"), (courseRole.CourseId, courseRole.Role));
    }

    [Fact]
    public async Task Me_ForStudent_HasTheCourseAsStudent()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var course = await TestData.AddCourseAsync(db, school.Id, group.Id);
        var student = await TestData.AddMemberAsync(db, group);

        var me = await GetMeAsync(student.Id);

        Assert.Equal("Student", me!.Role);
        var courseRole = Assert.Single(me.Courses);
        Assert.Equal((course.Id, "Student"), (courseRole.CourseId, courseRole.Role));
    }

    [Fact]
    public async Task Me_HasTheGroupsTheUserIsMemberOf_OrderedByName()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var second = await TestData.AddGroupAsync(db, school.Id);
        var first = await TestData.AddGroupAsync(db, school.Id);
        await TestData.AddGroupAsync(db, school.Id);
        second.Name = "Bbb " + second.Name;
        first.Name = "Aaa " + first.Name;
        var teacher = await TestData.AddUserAsync(db, school.Id, UserRole.Teacher);
        db.Enrollments.AddRange(
            new Enrollment { UserId = teacher.Id, GroupId = second.Id, Role = GroupRole.Student },
            new Enrollment { UserId = teacher.Id, GroupId = first.Id, Role = GroupRole.Teacher });
        await db.SaveChangesAsync();

        var me = await GetMeAsync(teacher.Id);

        Assert.Equal([first.Id, second.Id], me!.Groups.Select(group => group.Id));
        Assert.Equal([first.Name, second.Name], me.Groups.Select(group => group.Name));
    }

    [Fact]
    public async Task NotificationSettings_AreEmptyByDefault()
    {
        var userId = await AddUserAsync();

        var settings = await GetSettingsAsync(userId);

        Assert.Empty(settings!.Muted);
    }

    [Fact]
    public async Task NotificationSettings_RoundTrip_AndDuplicatesAreRemoved()
    {
        var userId = await AddUserAsync();

        var response = await PutSettingsAsync(userId, ["NewsPublished", "DeadlineSoon", "NewsPublished"]);

        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<NotificationSettingsResponse>();
        Assert.Equal(["NewsPublished", "DeadlineSoon"], saved!.Muted);
        var read = await GetSettingsAsync(userId);
        Assert.Equal(2, read!.Muted.Count);
        Assert.Contains("NewsPublished", read.Muted);
        Assert.Contains("DeadlineSoon", read.Muted);

        await PutSettingsAsync(userId, []);
        Assert.Empty((await GetSettingsAsync(userId))!.Muted);
    }

    [Fact]
    public async Task Me_ForUnknownUser_ReturnsNotFound()
    {
        var response = await SendAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> AddUserAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        return (await TestData.AddUserAsync(db, school.Id, UserRole.Student)).Id;
    }

    private async Task<NotificationSettingsResponse?> GetSettingsAsync(Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me/notification-settings");
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotificationSettingsResponse>();
    }

    private async Task<HttpResponseMessage> PutSettingsAsync(Guid userId, string[] muted)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/me/notification-settings")
        {
            Content = JsonContent.Create(new { muted })
        };
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<MeResponse?> GetMeAsync(Guid userId)
    {
        var response = await SendAsync(userId);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MeResponse>();
    }

    private async Task<HttpResponseMessage> SendAsync(Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        return await _client.SendAsync(request);
    }
}
