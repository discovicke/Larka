using System.Net.Http.Json;
using Backend.Application.Abstractions;
using Backend.Application.Services;
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
    public async Task Seed_GivesOneStudentAPersonalDeadline_ThatFollowsTheRules()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        var rows = await db.Submissions
            .Where(submission => submission.PersonalDueAt != null)
            .Select(submission => new { submission.Status, submission.PersonalDueAt, submission.ContentLinkId })
            .ToListAsync();

        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            var link = await db.ContentLinks.SingleAsync(candidate => candidate.Id == row.ContentLinkId);
            Assert.NotNull(link.DueAt);
            Assert.True(link.DueAt < _demoDay);
            Assert.True(row.PersonalDueAt > _demoDay);
            Assert.NotEqual(SubmissionStatus.Graded, row.Status);
        }
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
    public async Task Seed_GivesEveryCourseAClassAndCourseCode()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        var codes = await (
                from link in db.GroupCourses
                join grp in db.Groups on link.GroupId equals grp.Id
                join course in db.Courses on link.CourseId equals course.Id
                select new { Group = grp.Name, course.Code, course.Title })
            .ToListAsync();

        Assert.Equal(9, codes.Count);
        Assert.All(codes, row => Assert.StartsWith(row.Group + "_", row.Code));
        Assert.Equal(9, codes.Select(row => row.Title).Distinct().Count());
        Assert.Contains(codes, row => row.Code == ".NET25_DevOps" && row.Title == "DevOps");
    }

    [Fact]
    public async Task Seed_ReplacesOlderDemoData_AndKeepsTheAccounts()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var before = (
            await db.Users.CountAsync(),
            await db.Groups.CountAsync(),
            await db.Courses.CountAsync(),
            await db.Submissions.CountAsync());
        var studentId = (await db.Users.SingleAsync(user => user.Email == "elev")).Id;

        // Äldre demodata: klassen .NET25 med ett annat id, och en kurs som någon har lagt till själv.
        var marker = await db.Groups.SingleAsync(group => group.Id == DemoSeed.MarkerGroupId);
        var schoolId = marker.SchoolId;
        await db.ContentLinks.Where(link => link.GroupId == marker.Id).ExecuteDeleteAsync();
        await db.Enrollments.Where(row => row.GroupId == marker.Id).ExecuteDeleteAsync();
        await db.GroupCourses.Where(row => row.GroupId == marker.Id).ExecuteDeleteAsync();
        await db.Groups.Where(group => group.Id == marker.Id).ExecuteDeleteAsync();
        db.Groups.Add(new Backend.Domain.Entities.Group { SchoolId = schoolId, Name = DemoSeed.MarkerGroupName });
        db.Courses.Add(new Backend.Domain.Entities.Course { SchoolId = schoolId, Title = "Gammal kurs", Code = "OLD" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var seeded = await scope.ServiceProvider.GetRequiredService<DemoSeed>().SeedAsync(CancellationToken.None);

        Assert.True(seeded);
        Assert.False(await db.Courses.AnyAsync(course => course.Code == "OLD"));
        Assert.True(await db.Groups.AnyAsync(group => group.Id == DemoSeed.MarkerGroupId));
        Assert.Equal(studentId, (await db.Users.SingleAsync(user => user.Email == "elev")).Id);
        Assert.Equal(before, (
            await db.Users.CountAsync(),
            await db.Groups.CountAsync(),
            await db.Courses.CountAsync(),
            await db.Submissions.CountAsync()));
    }

    [Fact]
    public async Task Seed_GradesFollowTheDeadlineRules()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        var rows = await (
                from submission in db.Submissions
                join link in db.ContentLinks on submission.ContentLinkId equals link.Id
                join grade in db.Grades on submission.Id equals grade.SubmissionId
                select new { Submission = submission, Link = link, Grade = grade })
            .ToListAsync();

        Assert.Contains(rows, row => row.Grade.Level == GradeLevel.Fail);
        foreach (var row in rows)
        {
            Assert.True(row.Grade.GradedAt <= _demoDay);
            Assert.True(row.Submission.SubmittedAt <= row.Grade.GradedAt);
            if (row.Grade.Level == GradeLevel.Fail)
            {
                // IG kommer bara efter sista deadline, och texten säger inte åt eleven att komplettera.
                Assert.True(GradingWindow.Of(row.Link, row.Submission, row.Grade.GradedAt).AllowsFail);
                Assert.DoesNotContain("komplett", row.Grade.Feedback, StringComparison.OrdinalIgnoreCase);
            }

            if (row.Submission.Status == SubmissionStatus.NeedsWork)
            {
                Assert.Null(row.Grade.Level);
                Assert.True(GradingWindow.Of(row.Link, row.Submission, _demoDay).AllowsNeedsWork);
            }
        }
    }

    [Fact]
    public async Task Seed_GivesTheDemoStudentSomethingToMarkDoneAndAnOpenAssignment()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var student = await db.Users.SingleAsync(user => user.Email == "elev");
        var course = await db.Courses.SingleAsync(row => row.Code == ".NET25_DevOps");
        var visible = await (
                from link in db.ContentLinks
                join module in db.Modules on link.ModuleId equals module.Id
                join content in db.Content on link.ContentId equals content.Id
                where link.CourseId == course.Id && link.PublishAt <= _demoDay && module.PublishAt <= _demoDay
                select new { Link = link, content.Type })
            .ToListAsync();
        var done = await db.PartCompletions
            .Where(row => row.StudentId == student.Id)
            .Select(row => row.ContentLinkId)
            .ToListAsync();

        Assert.Contains(visible, row => row.Type == ContentType.Theory && !done.Contains(row.Link.Id));

        // Uppgiften är öppen mer än en vecka till, så demon fungerar även några dagar efter att datan skapats.
        var open = visible.Single(row => row.Type == ContentType.Assignment && row.Link.DueAt > _demoDay);
        Assert.True(open.Link.DueAt > _demoDay.AddDays(7));
        Assert.False(await db.Submissions.AnyAsync(submission => submission.ContentLinkId == open.Link.Id
            && submission.StudentId == student.Id
            && submission.Status != SubmissionStatus.NotStarted));

        // En annan elev har redan bett om hjälp, och läraren har fått en notis.
        Assert.True(await db.StudentHelpRequests.AnyAsync(request => request.ContentLinkId == open.Link.Id
            && request.StudentId != student.Id
            && request.ResolvedAt == null));
        var teacher = await db.Users.SingleAsync(user => user.Email == "larare");
        Assert.True(await db.Notifications.AnyAsync(notification => notification.UserId == teacher.Id
            && notification.Kind == NotificationKind.StudentHelpRequested));

        // Den avslutade kursen har betyg och feedback för demoeleven.
        Assert.True(await (
                from submission in db.Submissions
                join link in db.ContentLinks on submission.ContentLinkId equals link.Id
                join completed in db.Courses on link.CourseId equals completed.Id
                join grade in db.Grades on submission.Id equals grade.SubmissionId
                where completed.Code == ".NET25_WeApp" && submission.StudentId == student.Id
                select grade.Level)
            .AllAsync(level => level == GradeLevel.Pass || level == GradeLevel.Distinction));
    }

    [Fact]
    public async Task ConnectedGitHub_TurnsTheGitHubAssignmentIntoARepoAssignment_Once()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var seed = scope.ServiceProvider.GetRequiredService<DemoSeed>();
        var github = (TestGitHubAppService)scope.ServiceProvider.GetRequiredService<IGitHubAppService>();

        // Utan kopplad GitHub förblir uppgiften en textuppgift.
        Assert.Equal(0, await seed.EnableGitHubAssignmentAsync(CancellationToken.None));

        var school = await DemoSeed.FindSchoolAsync(db, CancellationToken.None);
        db.SchoolGitHubInstallations.Add(new Backend.Domain.Entities.SchoolGitHubInstallation
        {
            SchoolId = school!.Id,
            InstallationId = 42,
            Org = "skolan",
            Connected = true
        });
        await db.SaveChangesAsync();
        github.TemplateRepositories =
        [
            new GitHubRepository("skolan/gammal-mall", "", "main", true, true, IsArchived: true),
            new GitHubRepository("skolan/drifts-mall", "", "main", true, true, IsArchived: false),
        ];

        var created = await seed.EnableGitHubAssignmentAsync(CancellationToken.None);
        var again = await seed.EnableGitHubAssignmentAsync(CancellationToken.None);

        db.ChangeTracker.Clear();
        var link = await (
                from row in db.ContentLinks
                join content in db.Content on row.ContentId equals content.Id
                join course in db.Courses on row.CourseId equals course.Id
                where course.Code == ".NET25_DevOps" && content.Title == "Inlämning 4: Driftsätt appen"
                select row)
            .SingleAsync();
        var spec = await db.AssignmentSpecs.SingleAsync(row => row.ContentId == link.ContentId);
        var repos = await db.StudentRepos
            .Where(repo => db.Submissions.Any(submission =>
                submission.Id == repo.SubmissionId && submission.ContentLinkId == link.Id))
            .ToListAsync();

        Assert.Equal(8, created);
        Assert.Equal(0, again);
        Assert.Equal([SubmissionMode.Repo], spec.SubmissionModes);
        Assert.Equal("skolan/drifts-mall", spec.TemplateRepo);
        Assert.Equal(8, repos.Count);
        Assert.All(repos, repo => Assert.Equal(RepoProvisioningState.Pending, repo.ProvisioningState));
        Assert.Equal(8, repos.Select(repo => repo.DesiredRepoName).Distinct().Count());
        Assert.Contains(repos, repo => repo.DesiredRepoName == "devops-driftsatt-elev");
    }

    [Fact]
    public async Task DemoAccounts_CanReadTheOverviewThatThePresentationUses()
    {
        var admin = await GetAsync<OverviewResponse>("admin", "/api/me/admin-overview");
        Assert.Equal([".NET25", "FIBER25", "JAVA25"], admin.ClassTimelines.Select(item => item.Name).Order());

        var student = await GetAsync<StudyPlanResponse>("elev", "/api/me/study-plan");
        Assert.Contains(student.Courses, course => course.Code == ".NET25_DevOps");
        var teacher = await GetAsync<TeachingResponse>("larare", "/api/me/teaching");
        Assert.Contains(teacher.Courses, course => course.Code == ".NET25_DevOps");
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
