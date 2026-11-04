using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class StudentOverviewEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record OverviewResponse(Guid StudentId, string Name, Guid GroupId, List<CourseResponse> Courses);

    private record CourseResponse(
        Guid CourseId,
        string Title,
        string Code,
        int AssignmentCount,
        int NeedsWorkCount,
        int LateCount,
        int SubmittedCount,
        int GradedCount,
        int FailCount,
        int PassCount,
        int DistinctionCount);

    private record Scenario(
        Guid GroupId,
        Guid CourseId,
        Guid StudentId,
        Guid OtherStudentId,
        Guid TeacherId,
        Guid CourseTeacherId,
        Guid OtherSchoolAdminId);

    [Fact]
    public async Task Overview_AsAdmin_CountsTheStudentsStatusPerCourse()
    {
        var scenario = await AddScenarioAsync();
        var moduleId = (await _client.CreateModuleAsync(scenario.CourseId)).Id;
        var graded = await LinkAsync(scenario.CourseId, moduleId, "Godkänd");
        var distinction = await LinkAsync(scenario.CourseId, moduleId, "Väl godkänd", scale: "passFailDistinction");
        var needsWork = await LinkAsync(scenario.CourseId, moduleId, "Komplettera");
        var submitted = await LinkAsync(scenario.CourseId, moduleId, "Inlämnad");
        // Försenad: deadlinen har passerat men en kompletteringsdeadline återstår. Utan den blir det automatiskt IG.
        await LinkAsync(
            scenario.CourseId, moduleId, "Försenad", dueAt: "2026-02-01T00:00:00Z",
            resubmitDueAt: ["2099-03-01T00:00:00Z"]);
        await LinkAsync(scenario.CourseId, moduleId, "Automatiskt IG", dueAt: "2026-02-01T00:00:00Z");
        await LinkAsync(scenario.CourseId, moduleId, "Framtida", dueAt: "2099-02-01T00:00:00Z");
        await _client.GradeAsync(
            await _client.SubmitAsync(scenario.StudentId, graded),
            new { level = "pass", feedback = "Bra" });
        await _client.GradeAsync(
            await _client.SubmitAsync(
                scenario.StudentId,
                distinction), new { level = "distinction", feedback = "Utmärkt" });
        await _client.GradeAsync(
            await _client.SubmitAsync(scenario.StudentId, needsWork), new { feedback = "Mer", outcome = "needsWork" });
        await _client.SubmitAsync(scenario.StudentId, submitted);

        var response = await GetAsync(scenario, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var overview = (await response.Content.ReadFromJsonAsync<OverviewResponse>())!;
        Assert.Equal(scenario.StudentId, overview.StudentId);
        var course = Assert.Single(overview.Courses);
        Assert.Equal(scenario.CourseId, course.CourseId);
        Assert.Equal(
            (7, 1, 1, 1, 3, 1, 1, 1), // automatiskt IG räknas som bedömt och som IG
            (course.AssignmentCount, course.NeedsWorkCount, course.LateCount, course.SubmittedCount,
                course.GradedCount, course.FailCount, course.PassCount, course.DistinctionCount));
    }

    [Fact]
    public async Task Overview_OnlyCountsCoursesTheClassReads()
    {
        var scenario = await AddScenarioAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var otherGroup = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
            db.Enrollments.Add(new Backend.Domain.Entities.Enrollment
            {
                UserId = scenario.StudentId,
                GroupId = otherGroup.Id,
                Role = GroupRole.Student
            });
            await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, otherGroup.Id);
            await db.SaveChangesAsync();
        }

        var overview = (await (await GetAsync(scenario, null))
            .Content.ReadFromJsonAsync<OverviewResponse>())!;

        Assert.Equal(scenario.CourseId, Assert.Single(overview.Courses).CourseId);
    }

    [Fact]
    public async Task Overview_ForEveryoneElse_Returns404()
    {
        var scenario = await AddScenarioAsync();

        // Eleven själv, en klasskamrat, en lärare i klassen, en lärare bara i en av kurserna och admin i en
        // annan skola.
        foreach (var caller in new[]
        {
            scenario.StudentId, scenario.OtherStudentId, scenario.TeacherId, scenario.CourseTeacherId,
            scenario.OtherSchoolAdminId
        })
        {
            var response = await GetAsync(scenario, caller);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Overview_ForAMemberWhoIsNotAStudentInTheClass_Returns404()
    {
        var scenario = await AddScenarioAsync();

        var teacher = await GetAsync(scenario, null, scenario.TeacherId);
        var stranger = await GetAsync(scenario, null, Guid.NewGuid());
        var unknownGroup = await SendAsync(
            $"/api/groups/{Guid.NewGuid()}/students/{scenario.StudentId}/overview", null);

        Assert.Equal(HttpStatusCode.NotFound, teacher.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownGroup.StatusCode);
    }

    private Task<HttpResponseMessage> GetAsync(Scenario scenario, Guid? caller, Guid? studentId = null) =>
        SendAsync($"/api/groups/{scenario.GroupId}/students/{studentId ?? scenario.StudentId}/overview", caller);

    private async Task<HttpResponseMessage> SendAsync(string path, Guid? caller)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (caller is not null)
        {
            request.Headers.Add(TestAuthHandler.Header, caller.Value.ToString());
        }

        return await _client.SendAsync(request);
    }

    private async Task<Scenario> AddScenarioAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var course = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, group.Id);
        var student = await TestData.AddMemberAsync(db, group);
        var otherStudent = await TestData.AddMemberAsync(db, group);
        var teacher = await TestData.AddMemberAsync(db, group, GroupRole.Teacher, UserRole.Teacher);
        var courseTeacher = await TestData.AddCourseTeacherAsync(db, course);
        var otherSchoolAdmin = await TestData.AddUserAsync(db, role: UserRole.Admin);
        return new Scenario(
            group.Id, course.Id, student.Id, otherStudent.Id, teacher.Id, courseTeacher.Id,
            otherSchoolAdmin.Id);
    }

    // Länkar en uppgift som redan är publicerad, med en tom lista omlämningsdeadlines som standard.
    private Task<Guid> LinkAsync(
        Guid courseId,
        Guid moduleId,
        string title,
        string? dueAt = null,
        string scale = "passFail",
        string[]? resubmitDueAt = null) =>
        _client.LinkAssignmentAsync(
            courseId,
            moduleId,
            title,
            publishAt: "2026-01-01T00:00:00Z",
            dueAt: dueAt,
            gradeScale: scale,
            resubmitDueAt: resubmitDueAt ?? []);
}
