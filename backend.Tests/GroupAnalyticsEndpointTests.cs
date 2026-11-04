using System.Net;
using System.Net.Http.Json;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class GroupAnalyticsEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record AnalyticsResponse(List<CourseResponse> Courses);

    private record CourseResponse(
        Guid CourseId,
        int GradedCount,
        int FailCount,
        int PassCount,
        int OverdueCount,
        int AutoFailCount);

    private record SubmissionRow(Guid Id, Guid StudentId, string Status, DeadlineRow? Deadline);

    private record DeadlineRow(bool AutoFail, bool CanHandIn);

    private record Scenario(Guid GroupId, Guid CourseId, Guid ModuleId, Guid StudentId);

    [Fact]
    public async Task Overdue_IsOnlyCountedWhileADeadlineRemains_OtherwiseItIsAutomaticFail()
    {
        var scenario = await AddScenarioAsync();
        // Klassens deadline har passerat men en kompletteringsdeadline återstår: försenad.
        await LinkAsync(scenario, "Försenad", "2026-02-01T00:00:00Z", ["2099-03-01T00:00:00Z"]);
        // Ingen deadline återstår: automatiskt IG, inte försenad.
        await LinkAsync(scenario, "Automatiskt IG", "2026-02-01T00:00:00Z", []);

        var course = await GetCourseAsync(scenario);

        Assert.Equal((1, 1), (course.OverdueCount, course.AutoFailCount));
    }

    [Fact]
    public async Task AutomaticFail_CountsAsGradedAndAsFail()
    {
        var scenario = await AddScenarioAsync();
        var passed = await _client.LinkAssignmentAsync(
            scenario.CourseId, scenario.ModuleId, "Godkänd", publishAt: "2026-01-01T00:00:00Z");
        await LinkAsync(scenario, "Automatiskt IG", "2026-02-01T00:00:00Z", []);
        await _client.LinkAssignmentAsync(
            scenario.CourseId, scenario.ModuleId, "Öppen", publishAt: "2026-01-01T00:00:00Z",
            dueAt: "2099-02-01T00:00:00Z");
        await _client.GradeAsync(
            await _client.SubmitAsync(scenario.StudentId, passed), new { level = "pass", feedback = "Bra" });

        var course = await GetCourseAsync(scenario);

        Assert.Equal((2, 1, 1, 1), (course.GradedCount, course.PassCount, course.FailCount, course.AutoFailCount));
    }

    [Fact]
    public async Task APersonalDeadline_LiftsAutomaticFail_AndTheStudentCanHandIn()
    {
        var scenario = await AddScenarioAsync();
        var link = await LinkAsync(scenario, "Automatiskt IG", "2026-02-01T00:00:00Z", []);
        Assert.True((await GetRowAsync(link, scenario.StudentId)).Deadline!.AutoFail);
        Assert.Equal(1, (await GetCourseAsync(scenario)).AutoFailCount);

        var response = await _client.PutAsJsonAsync(
            $"/api/links/{link}/students/{scenario.StudentId}/personal-due-at",
            new { dueAt = DateTimeOffset.UtcNow.AddDays(2) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lifted = await GetRowAsync(link, scenario.StudentId);
        Assert.Equal((false, true), (lifted.Deadline!.AutoFail, lifted.Deadline.CanHandIn));
        var course = await GetCourseAsync(scenario);
        Assert.Equal((0, 0, 0, 1), (course.AutoFailCount, course.GradedCount, course.FailCount, course.OverdueCount));
        await _client.SubmitAsync(scenario.StudentId, link);
        Assert.Equal("Submitted", (await GetRowAsync(link, scenario.StudentId)).Status);
    }

    private async Task<SubmissionRow> GetRowAsync(Guid linkId, Guid studentId) =>
        (await _client.GetJsonAsync<List<SubmissionRow>>($"/api/links/{linkId}/submissions"))
        .Single(row => row.StudentId == studentId);

    private async Task<CourseResponse> GetCourseAsync(Scenario scenario)
    {
        var analytics = await _client.GetJsonAsync<AnalyticsResponse>($"/api/groups/{scenario.GroupId}/analytics");
        return analytics.Courses.Single(course => course.CourseId == scenario.CourseId);
    }

    private Task<Guid> LinkAsync(Scenario scenario, string title, string dueAt, string[] resubmitDueAt) =>
        _client.LinkAssignmentAsync(
            scenario.CourseId, scenario.ModuleId, title, publishAt: "2026-01-01T00:00:00Z", dueAt: dueAt,
            resubmitDueAt: resubmitDueAt);

    private async Task<Scenario> AddScenarioAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db, PlaceholderIds.SchoolId);
        var course = await TestData.AddCourseAsync(db, PlaceholderIds.SchoolId, group.Id);
        var student = await TestData.AddMemberAsync(db, group);
        var module = await _client.CreateModuleAsync(course.Id);
        return new Scenario(group.Id, course.Id, module.Id, student.Id);
    }
}
