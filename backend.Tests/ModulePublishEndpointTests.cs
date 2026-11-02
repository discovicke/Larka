using System.Net;
using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class ModulePublishEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private record PublishResponse(int WeekCount, int PartCount);

    private record WeekResponse(Guid Id, List<PartResponse> Content);

    private record PartResponse(Guid ContentId, string Status);

    private record Scenario(Guid CourseId, Guid TeacherId, Guid StudentId, Guid WeekOne, Guid WeekTwo);

    [Fact]
    public async Task Publish_WithoutWeeks_PublishesAllDraftsInTheCourse()
    {
        var scenario = await AddScenarioAsync();

        var response = await PublishAsync(scenario, scenario.TeacherId, new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PublishResponse>();
        Assert.Equal(new PublishResponse(2, 2), result);
        var weeks = await GetWeeksAsync(scenario, scenario.TeacherId);
        Assert.All(weeks, week => Assert.Equal("Published", week.Content.Single().Status));
    }

    [Fact]
    public async Task Publish_WithAWeek_LeavesOtherWeeksAsDrafts()
    {
        var scenario = await AddScenarioAsync();

        var response = await PublishAsync(scenario, scenario.TeacherId, new { moduleIds = new[] { scenario.WeekOne } });

        var result = await response.Content.ReadFromJsonAsync<PublishResponse>();
        Assert.Equal(new PublishResponse(1, 1), result);
        var weeks = await GetWeeksAsync(scenario, scenario.TeacherId);
        Assert.Equal("Published", weeks.Single(week => week.Id == scenario.WeekOne).Content.Single().Status);
        Assert.Equal("Draft", weeks.Single(week => week.Id == scenario.WeekTwo).Content.Single().Status);
    }

    [Fact]
    public async Task Publish_WithAWeekFromAnotherCourse_ReturnsBadRequest()
    {
        var scenario = await AddScenarioAsync();

        var response = await PublishAsync(scenario, scenario.TeacherId, new { moduleIds = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Publish_AsAdminOrStudent_IsForbidden()
    {
        var scenario = await AddScenarioAsync();

        var admin = await PublishAsync(scenario, Guid.Empty, new { });
        var student = await PublishAsync(scenario, scenario.StudentId, new { });

        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, student.StatusCode);
    }

    [Fact]
    public async Task Weeks_WithOnlyDrafts_AreHiddenFromStudentsUntilPublished()
    {
        var scenario = await AddScenarioAsync();

        var before = await GetWeeksAsync(scenario, scenario.StudentId);
        await PublishAsync(scenario, scenario.TeacherId, new { moduleIds = new[] { scenario.WeekOne } });
        var after = await GetWeeksAsync(scenario, scenario.StudentId);

        Assert.Empty(before);
        Assert.Equal(scenario.WeekOne, Assert.Single(after).Id);
    }

    private async Task<HttpResponseMessage> PublishAsync(Scenario scenario, Guid asUser, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/courses/{scenario.CourseId}/modules/publish")
        {
            Content = JsonContent.Create(body)
        };
        AddUser(request, asUser);
        return await _client.SendAsync(request);
    }

    private async Task<List<WeekResponse>> GetWeeksAsync(Scenario scenario, Guid asUser)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/courses/{scenario.CourseId}/modules");
        AddUser(request, asUser);
        var response = await _client.SendAsync(request);
        return (await response.Content.ReadFromJsonAsync<List<WeekResponse>>())!;
    }

    // Guid.Empty betyder placeholder-användaren, som är Admin.
    private static void AddUser(HttpRequestMessage request, Guid asUser)
    {
        if (asUser != Guid.Empty)
        {
            request.Headers.Add(TestAuthHandler.Header, asUser.ToString());
        }
    }

    private AppDb OpenDb() => factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDb>();

    private async Task<Scenario> AddScenarioAsync()
    {
        await using var db = OpenDb();
        var school = await db.Schools.SingleAsync(found => found.Id == PlaceholderIds.SchoolId);
        var group = await TestData.AddGroupAsync(db, school.Id);
        var student = await TestData.AddMemberAsync(db, group);
        var course = await TestData.AddCourseAsync(db, school.Id, group.Id, placeholderTeaches: false);
        var teacher = await TestData.AddCourseTeacherAsync(db, course);
        var weekOne = AddWeekWithDraft(db, course, teacher, 1);
        var weekTwo = AddWeekWithDraft(db, course, teacher, 2);
        await db.SaveChangesAsync();
        return new Scenario(course.Id, teacher.Id, student.Id, weekOne.Id, weekTwo.Id);
    }

    private static Module AddWeekWithDraft(AppDb db, Course course, User teacher, int position)
    {
        var week = new Module { CourseId = course.Id, Title = $"Vecka {position}", Position = position };
        var content = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.Theory,
            Title = $"Teori {position}",
            Status = ContentStatus.Draft
        };
        db.Modules.Add(week);
        db.Content.Add(content);
        db.ContentLinks.Add(new ContentLink
        {
            ContentId = content.Id,
            CourseId = course.Id,
            ModuleId = week.Id,
            Position = 1
        });
        return week;
    }
}
