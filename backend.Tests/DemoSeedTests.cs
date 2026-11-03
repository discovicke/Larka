using System.Net.Http.Json;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace backend.Tests;

/// <summary>Den kompakta demodata som används för den korta demonstrationen.</summary>
public class DemoSeedTests(DemoSeedTests.DemoFixture fixture) : IClassFixture<DemoSeedTests.DemoFixture>
{
    private static readonly DateTimeOffset _demoDay = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    public sealed class DemoFixture : IDisposable
    {
        private readonly TestWebApplicationFactory _database = new();

        public DemoFixture()
        {
            Factory = _database.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:SeedDemoData", "true");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(_demoDay));
                });
            });
            _ = Factory.Server;
        }

        public WebApplicationFactory<Program> Factory { get; }

        public void Dispose()
        {
            Factory.Dispose();
            _database.Dispose();
        }
    }

    [Fact]
    public async Task Seed_CreatesThreeSmallClassesWithThreeCoursesEach()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        var groups = await db.Groups.OrderBy(group => group.Name).ToListAsync();
        var courses = await db.Courses.ToListAsync();

        Assert.Equal(3, groups.Count);
        Assert.All([".NET25", "FIBER25", "JAVA25"], name => Assert.Contains(groups, group => group.Name == name));
        Assert.Equal(9, courses.Count);
        Assert.DoesNotContain(groups, group => group.Name == AccountSeed.ClassName);

        foreach (var group in groups)
        {
            var students = await db.Enrollments.CountAsync(
                enrollment => enrollment.GroupId == group.Id && enrollment.Role == GroupRole.Student);
            var courseIds = await db.GroupCourses
                .Where(link => link.GroupId == group.Id)
                .Select(link => link.CourseId)
                .ToListAsync();
            var statuses = await db.Courses
                .Where(course => courseIds.Contains(course.Id))
                .Select(course => new { course.StartsOn, course.EndsOn })
                .ToListAsync();

            Assert.Equal(8, students);
            Assert.Equal(3, statuses.Count);
            Assert.Single(statuses, course => course.EndsOn < DateOnly.FromDateTime(_demoDay.UtcDateTime));
            Assert.Single(statuses, course =>
                course.StartsOn <= DateOnly.FromDateTime(_demoDay.UtcDateTime)
                && course.EndsOn >= DateOnly.FromDateTime(_demoDay.UtcDateTime));
            Assert.Single(statuses, course => course.StartsOn > DateOnly.FromDateTime(_demoDay.UtcDateTime));
        }
    }

    [Fact]
    public async Task Seed_KeepsThePublicAccountsAndCreatesWorkToDemo()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await db.Groups.SingleAsync(row => row.Name == DemoSeed.MarkerGroupName);
        var teacher = await db.Users.SingleAsync(user => user.Email == "larare");
        var student = await db.Users.SingleAsync(user => user.Email == "elev");

        Assert.True(await db.Enrollments.AnyAsync(row =>
            row.GroupId == group.Id && row.UserId == teacher.Id && row.Role == GroupRole.Teacher));
        Assert.True(await db.Enrollments.AnyAsync(row =>
            row.GroupId == group.Id && row.UserId == student.Id && row.Role == GroupRole.Student));
        Assert.True(await db.Grades.AnyAsync());
        Assert.True(await db.Submissions.AnyAsync(submission => submission.Status == SubmissionStatus.Submitted));
        Assert.True(await db.Submissions.AnyAsync(submission => submission.Status == SubmissionStatus.NeedsWork));
        Assert.True(await db.ContentLinks.AnyAsync(link => link.GroupId == group.Id));
    }

    [Fact]
    public async Task Seed_IsIdempotent()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var before = (
            await db.Users.CountAsync(),
            await db.Groups.CountAsync(),
            await db.Courses.CountAsync(),
            await db.Submissions.CountAsync());

        var seededAgain = await scope.ServiceProvider.GetRequiredService<DemoSeed>()
            .SeedAsync(CancellationToken.None);

        Assert.False(seededAgain);
        Assert.Equal(before, (
            await db.Users.CountAsync(),
            await db.Groups.CountAsync(),
            await db.Courses.CountAsync(),
            await db.Submissions.CountAsync()));
    }

    [Fact]
    public async Task DemoAccounts_CanReadTheOverviewThatThePresentationUses()
    {
        var admin = await GetAsync<OverviewResponse>("admin", "/api/me/admin-overview");
        Assert.Equal([".NET25", "FIBER25", "JAVA25"], admin.ClassTimelines.Select(item => item.Name).Order());

        var student = await GetAsync<StudyPlanResponse>("elev", "/api/me/study-plan");
        Assert.Contains(student.Courses, course => course.Code == "NET-NUT");
        var teacher = await GetAsync<TeachingResponse>("larare", "/api/me/teaching");
        Assert.Contains(teacher.Courses, course => course.Code == "NET-NUT");
    }

    private async Task<T> GetAsync<T>(string username, string url)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var userId = (await db.Users.SingleAsync(user => user.Email == username)).Id;
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthHandler.Header, userId.ToString());
        var response = await fixture.Factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private sealed record OverviewResponse(List<ClassTimeline> ClassTimelines);

    private sealed record ClassTimeline(string Name);

    private sealed record StudyPlanResponse(List<StudyCourse> Courses);

    private sealed record StudyCourse(string Code);

    private sealed record TeachingResponse(List<TeachingCourse> Courses);

    private sealed record TeachingCourse(string Code);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
