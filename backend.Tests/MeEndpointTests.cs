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
        Guid Id, string Name, string Email, string Role, SchoolResponse School, List<CourseRoleResponse> Courses);

    private record SchoolResponse(Guid Id, string Name);

    private record CourseRoleResponse(Guid CourseId, string Role);

    [Fact]
    public async Task Me_ForAdmin_HasEveryCourseInTheSchoolAsTeacher()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        var admin = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
        var first = await TestData.AddCourseAsync(db, school.Id);
        var second = await TestData.AddCourseAsync(db, school.Id);

        var me = await GetMeAsync(admin.Id);

        Assert.Equal(("Admin", admin.Name, admin.Email), (me!.Role, me.Name, me.Email));
        Assert.Equal((school.Id, school.Name), (me.School.Id, me.School.Name));
        Assert.Equal(2, me.Courses.Count);
        Assert.Contains(me.Courses, course => course.CourseId == first.Id && course.Role == "Teacher");
        Assert.Contains(me.Courses, course => course.CourseId == second.Id && course.Role == "Teacher");
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
        db.Enrollments.AddRange(
            new Enrollment { UserId = teacher.Id, GroupId = group.Id, Role = GroupRole.Teacher },
            // Samma kurs via en annan grupp, där läraren bara är elev: Teacher vinner.
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
    public async Task Me_ForUnknownUser_ReturnsNotFound()
    {
        var response = await SendAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
