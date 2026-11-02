using System.Net.Http.Json;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace backend.Tests;

/// <summary>
/// Demodatan (DemoSeed). Fixturen startar appen med Database:SeedDemoData på och klockan satt till demodagen
/// (måndag 2026-11-02), precis som i prod, och de flesta testerna läser bara resultatet. De som behöver ett annat
/// utgångsläge startar en egen app.
/// </summary>
public class DemoSeedTests(DemoSeedTests.DemoFixture fixture) : IClassFixture<DemoSeedTests.DemoFixture>
{
    private static readonly DateTimeOffset _demoDay = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    private record MeResponse(Guid Id, string Email, string Role, List<CourseRoleResponse> Courses);

    private record CourseRoleResponse(Guid CourseId, string Role);

    private record StudyPlanResponse(int CompletedPoints, int TotalPoints, List<StudyCourseResponse> Courses);

    private record StudyCourseResponse(
        string Code, string Title, string Status, int? Points, int AssignmentCount, int GradedCount,
        List<StudyAssignmentResponse> Assignments);

    private record StudyAssignmentResponse(string Title, string Status, string? Grade, string? Feedback);

    private record TodayResponse(
        List<TodoResponse> Todo, List<TodayFeedbackResponse> Feedback, List<TodayNewsResponse> News);

    private record TodoResponse(string Title, DateTimeOffset DueAt);

    private record TodayFeedbackResponse(string Title, string? Grade, string Feedback);

    private record TodayNewsResponse(string Title, string? Source);

    private record TeachingResponse(
        List<ToGradeResponse> ToGrade, int ToGradeTotal, List<DeadlineResponse> Deadlines,
        List<ScheduledResponse> Scheduled, List<TeachingCourseResponse> Courses);

    private record ToGradeResponse(string Title, string CourseTitle, string StudentName);

    private record DeadlineResponse(string Title, int Submitted, int Students);

    private record ScheduledResponse(string Title, string Kind);

    private record TeachingCourseResponse(string Code, int Students, int ToGrade);

    private record GroupResponse(string Name, string Type, string Term, int MemberCount);

    private record NotificationListResponse(List<NotificationResponse> Items, int UnreadCount);

    private record NotificationResponse(string Kind, DateTimeOffset? ReadAt);

    /// <summary>Appen med demodata påslagen och fast klocka. Basfabriken äger databasen och släpper den efteråt.</summary>
    public sealed class DemoFixture : IDisposable
    {
        private readonly TestWebApplicationFactory _database = new();

        public DemoFixture()
        {
            Factory = CreateApp(_database);
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
    public async Task Seed_OnEmptyDatabase_CreatesClassesCoursesAndPeople()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        var school = await db.Schools.SingleAsync(row => row.Id == Backend.Infrastructure.Persistence.Configurations
            .PlaceholderIds.SchoolId);
        var groups = await db.Groups.ToListAsync();
        var courses = await db.Courses.ToListAsync();

        Assert.Equal("Edugrade", school.Name);
        Assert.Equal(
            [".NET24", ".NET25", "FIB25", "Klassråd .NET25"],
            groups.Select(group => group.Name).Order());
        Assert.Equal(GroupType.Committee, groups.Single(group => group.Name == "Klassråd .NET25").Type);
        Assert.Equal(15, courses.Count);
        Assert.Contains(courses, course => course.Code == ".NET25_DEVOPS" && course.Points == 25
            && course.StartsOn == new DateOnly(2026, 10, 12) && course.EndsOn == new DateOnly(2026, 11, 13));
        Assert.DoesNotContain(courses, course => course.Code == AccountSeed.CourseCode);
        Assert.DoesNotContain(groups, group => group.Name == AccountSeed.ClassName);

        foreach (var className in new[] { ".NET25", ".NET24", "FIB25" })
        {
            var group = groups.Single(row => row.Name == className);
            var roles = await db.Enrollments.Where(enrollment => enrollment.GroupId == group.Id)
                .GroupBy(enrollment => enrollment.Role)
                .ToDictionaryAsync(rows => rows.Key, rows => rows.Count());
            Assert.InRange(roles[GroupRole.Student], 18, 24);
            Assert.InRange(roles[GroupRole.Teacher], 3, 4);
            Assert.Equal(1, roles[GroupRole.Mentor]);
            Assert.True(await db.GroupCourses.AnyAsync(link => link.GroupId == group.Id));
        }

        var committeeId = groups.Single(group => group.Type == GroupType.Committee).Id;
        Assert.False(await db.GroupCourses.AnyAsync(link => link.GroupId == committeeId));
    }

    [Fact]
    public async Task Seed_CreatesUniqueAsciiEmails_AndOneSharedPassword()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var seeded = await db.Users.Where(user => user.Email.EndsWith("@demo.larka.se")).ToListAsync();

        Assert.True(seeded.Count >= 55);
        Assert.Equal(seeded.Count, seeded.Select(user => user.Email).Distinct().Count());
        Assert.All(seeded, user => Assert.Matches("^[a-z]+(\\.[a-z]+)+@demo\\.larka\\.se$", user.Email));
        Assert.Contains(seeded, user => user.Name == "Karin Holmgren" && user.Email == "karin.holmgren@demo.larka.se");
        Assert.Contains(seeded, user => user.Name == "Anders Wikström"
            && user.Email == "anders.wikstrom@demo.larka.se");

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        Assert.All(seeded, user => Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(user, user.PasswordHash!, DemoSeed.DemoPassword)));
    }

    [Fact]
    public async Task Seed_PlacesThePublicAccountsInTheRealClass()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await db.Groups.SingleAsync(row => row.Name == DemoSeed.MarkerGroupName);

        var teacher = await db.Users.SingleAsync(user => user.Email == "larare");
        var student = await db.Users.SingleAsync(user => user.Email == "elev");
        var admin = await db.Users.SingleAsync(user => user.Email == "admin");

        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal(GroupRole.Teacher, (await db.Enrollments.SingleAsync(
            enrollment => enrollment.UserId == teacher.Id && enrollment.GroupId == group.Id)).Role);
        Assert.Equal(GroupRole.Student, (await db.Enrollments.SingleAsync(
            enrollment => enrollment.UserId == student.Id && enrollment.GroupId == group.Id)).Role);

        var me = await GetAsync<MeResponse>("larare", "/api/me");
        var devOps = await db.Courses.SingleAsync(course => course.Code == ".NET25_DEVOPS");
        Assert.Contains(me.Courses, course => course.CourseId == devOps.Id && course.Role == "Teacher");
    }

    // Varje kurs har sina egna lärare, och katalogens författare är en av dem. larare undervisar i DevOps och
    // Webbapplikationer i C# i .NET25, men inte i klassens övriga kurser trots att hen är lärare i klassen.
    [Fact]
    public async Task Seed_GivesEveryCourseItsOwnTeachers_AndLarareTeachesTwoOfTheClassCourses()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var codeOf = await db.Courses.ToDictionaryAsync(course => course.Id, course => course.Code);
        var emailOf = await db.Users.ToDictionaryAsync(user => user.Id, user => user.Email);
        var taught = (await db.CourseTeachers.ToListAsync())
            .GroupBy(row => codeOf[row.CourseId])
            .ToDictionary(rows => rows.Key, rows => rows.Select(row => emailOf[row.UserId]).Order().ToList());

        foreach (var course in DemoCatalog.Classes().SelectMany(spec => spec.Courses))
        {
            Assert.Contains(course.Author, course.Teachers);
            Assert.Equal(
                course.Teachers.Select(key => key == "larare"
                        ? "larare"
                        : DemoPeople.Teachers.Concat(DemoPeople.Mentors).Single(person => person.Key == key).Email)
                    .Order(),
                taught[course.Code]);
        }

        var me = await GetAsync<MeResponse>("larare", "/api/me");
        var teaching = me.Courses.Where(course => course.Role == "Teacher")
            .Select(course => codeOf[course.CourseId]).Order();
        Assert.Equal([".NET25_DEVOPS", ".NET25_WEBB"], teaching);
    }

    [Fact]
    public async Task Seed_RunAgain_CreatesNothingNew()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var before = await CountAllAsync(db);

        var seeded = await scope.ServiceProvider.GetRequiredService<DemoSeed>().SeedAsync(CancellationToken.None);
        await fixture.Factory.Services.SeedDemoDataAsync();
        await fixture.Factory.Services.SeedAccountsAsync();

        Assert.False(seeded);
        Assert.Equal(before, await CountAllAsync(db));
        Assert.False(await db.Groups.AnyAsync(group => group.Name == AccountSeed.ClassName));
    }

    [Fact]
    public async Task Seed_GradesEveryStudent_OnFinishedCourses()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await db.Groups.SingleAsync(row => row.Name == DemoSeed.MarkerGroupName);
        var studentIds = await db.Enrollments
            .Where(enrollment => enrollment.GroupId == group.Id && enrollment.Role == GroupRole.Student)
            .Select(enrollment => enrollment.UserId)
            .ToListAsync();

        var finished = await db.Courses.Where(course => course.Code == ".NET25_CSHARP").SingleAsync();
        var assignmentLinks = await db.ContentLinks
            .Where(link => link.CourseId == finished.Id && link.DueAt != null)
            .Select(link => link.Id)
            .ToListAsync();
        var graded = await db.Submissions
            .Where(submission => assignmentLinks.Contains(submission.ContentLinkId)
                && submission.Status == SubmissionStatus.Graded && submission.Grade!.Level != null)
            .ToListAsync();

        Assert.Equal(3, assignmentLinks.Count);
        Assert.Equal(studentIds.Count * assignmentLinks.Count, graded.Count);
    }

    [Fact]
    public async Task StudentPages_ShowHistoryDeadlinesAndFeedback()
    {
        var plan = await GetAsync<StudyPlanResponse>("elev", "/api/me/study-plan");
        var today = await GetAsync<TodayResponse>("elev", "/api/me/today");
        var notifications = await GetAsync<NotificationListResponse>("elev", "/api/notifications");

        Assert.Equal(
            [
                "Programmering med C#", "Databaser", "Webbapplikationer i C#", "DevOps",
                "AI-komponenter och ML i Azure", "LIA 1"
            ],
            plan.Courses.Select(course => course.Title));
        Assert.Equal(95, plan.CompletedPoints);
        var devOps = plan.Courses.Single(course => course.Code == ".NET25_DEVOPS");
        Assert.Equal("Ongoing", devOps.Status);
        // Veckan med den sista uppgiften öppnar först fredag, så den syns inte än.
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["Inlämning: Git-flöde i teamet"] = "Graded",
                ["Laboration: CI/CD"] = "NeedsWork",
                ["Laboration: Docker och containrar"] = "Submitted",
                ["Laboration: Testautomation i CI"] = "NotStarted"
            },
            devOps.Assignments.ToDictionary(assignment => assignment.Title, assignment => assignment.Status));
        Assert.All(
            plan.Courses.Where(course => course.Status == "Completed"),
            course => Assert.Equal(course.AssignmentCount, course.GradedCount));

        // Något ska in i veckan och något längre fram. En komplettering står kvar även efter deadline, och
        // veckan med sista uppgiften öppnar först fredag.
        Assert.Equal(
            ["Laboration: CI/CD", "Laboration: Testautomation i CI", "Inlämning: Vald LIA-plats"],
            today.Todo.Select(item => item.Title));
        Assert.Contains(today.Feedback, item => item.Title == "Laboration: CI/CD");
        Assert.NotEmpty(today.News);
        Assert.Contains(notifications.Items, item => item.Kind == "SubmissionNeedsWork" && item.ReadAt is null);
        Assert.Contains(notifications.Items, item => item.ReadAt is not null);
    }

    [Fact]
    public async Task TeacherPages_ShowAGradingQueueDeadlinesAndTheClasses()
    {
        var teaching = await GetAsync<TeachingResponse>("larare", "/api/me/teaching");
        var groups = await GetAsync<List<GroupResponse>>("larare", "/api/groups");

        Assert.True(teaching.ToGradeTotal >= 8);
        Assert.Equal(8, teaching.ToGrade.Count);
        Assert.All(teaching.ToGrade, item => Assert.Equal("DevOps", item.CourseTitle));
        Assert.Contains(
            teaching.Deadlines,
            item => item.Title == "Laboration: Testautomation i CI" && item.Students >= 18);
        Assert.Contains(teaching.Scheduled, item => item.Kind == "Week" && item.Title.StartsWith("Vecka 5 –"));
        Assert.Equal(".NET25_DEVOPS", teaching.Courses.Single(course => course.ToGrade > 0).Code);
        Assert.Contains(groups, group => group.Name == ".NET25" && group.Type == "Class" && group.MemberCount >= 25);
        // Läraren ser bara sina egna grupper, inte andra klasser i skolan.
        Assert.DoesNotContain(groups, group => group.Name == "FIB25");
        Assert.Contains(groups, group => group.Type == "Committee");
    }

    [Fact]
    public async Task Seed_CoversEverySubmissionState_AndTheDemoDatesAroundMonday()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var devOps = await db.Courses.SingleAsync(course => course.Code == ".NET25_DEVOPS");
        var links = await db.ContentLinks
            .Where(link => link.CourseId == devOps.Id && link.DueAt != null)
            .ToListAsync();

        Assert.Contains(links, link => link.DueAt < _demoDay);
        Assert.Contains(links, link => link.DueAt > _demoDay && link.DueAt < _demoDay.AddDays(6));
        Assert.Contains(links, link => link.DueAt > _demoDay.AddDays(7));

        var statuses = await db.Submissions
            .Where(submission => db.ContentLinks.Any(link => link.Id == submission.ContentLinkId
                && link.CourseId == devOps.Id))
            .Select(submission => submission.Status)
            .Distinct()
            .ToListAsync();
        Assert.Contains(SubmissionStatus.Submitted, statuses);
        Assert.Contains(SubmissionStatus.NeedsWork, statuses);
        Assert.Contains(SubmissionStatus.Graded, statuses);

        var needsWork = await db.Grades.Where(grade => grade.Level == null).ToListAsync();
        Assert.NotEmpty(needsWork);
        Assert.All(needsWork, grade => Assert.False(string.IsNullOrWhiteSpace(grade.Feedback)));
        Assert.True(await db.Content.AnyAsync(content => content.Type == ContentType.News));
        Assert.True(await db.Comments.AnyAsync());
        Assert.True(await db.PartCompletions.AnyAsync());
        Assert.False(await db.StudentRepos.AnyAsync());
        Assert.False(await db.SchoolGitHubInstallations.AnyAsync());
    }

    [Fact]
    public async Task Seed_RemovesDemoClassAndCourse_WhenNobodySubmittedThere()
    {
        using var database = new TestWebApplicationFactory();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.True(await db.Groups.AnyAsync(group => group.Name == AccountSeed.ClassName));
        var demoCourse = await db.Courses.SingleAsync(course => course.Code == AccountSeed.CourseCode);

        await scope.ServiceProvider.GetRequiredService<DemoSeed>().SeedAsync(CancellationToken.None);

        Assert.False(await db.Groups.AnyAsync(group => group.Name == AccountSeed.ClassName));
        Assert.False(await db.Courses.AnyAsync(course => course.Code == AccountSeed.CourseCode));
        Assert.False(await db.Modules.AnyAsync(module => module.CourseId == demoCourse.Id));
        Assert.False(await db.Content.AnyAsync(content => content.Title == "Så fungerar en kurs"));
        Assert.False(await db.GroupCourses.AnyAsync(link => link.CourseId == demoCourse.Id));

        // Nästa start ska inte skapa dem igen.
        await scope.ServiceProvider.GetRequiredService<AccountSeed>().SeedAsync(CancellationToken.None);
        Assert.False(await db.Groups.AnyAsync(group => group.Name == AccountSeed.ClassName));
        Assert.False(await db.Courses.AnyAsync(course => course.Code == AccountSeed.CourseCode));
        Assert.Equal(
            GroupRole.Student,
            (await db.Enrollments.SingleAsync(enrollment => db.Users.Any(
                user => user.Id == enrollment.UserId && user.Email == "elev")
                && db.Groups.Any(group => group.Id == enrollment.GroupId && group.Name == ".NET25"))).Role);
    }

    [Fact]
    public async Task Seed_KeepsDemoClassAndCourse_WhenSomeoneSubmittedThere()
    {
        using var database = new TestWebApplicationFactory();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var student = await db.Users.SingleAsync(user => user.Email == "elev");
        var assignmentLink = await db.ContentLinks.SingleAsync(link =>
            db.Content.Any(content => content.Id == link.ContentId && content.Type == ContentType.Assignment));
        db.Submissions.Add(new Submission
        {
            ContentLinkId = assignmentLink.Id,
            StudentId = student.Id,
            Status = SubmissionStatus.Submitted,
            BodyMd = "Min reflektion.",
            SubmittedAt = _demoDay
        });
        await db.SaveChangesAsync();

        var seeded = await scope.ServiceProvider.GetRequiredService<DemoSeed>().SeedAsync(CancellationToken.None);

        Assert.True(seeded);
        Assert.True(await db.Groups.AnyAsync(group => group.Name == AccountSeed.ClassName));
        Assert.True(await db.Courses.AnyAsync(course => course.Code == AccountSeed.CourseCode));
        Assert.True(await db.Submissions.AnyAsync(submission => submission.ContentLinkId == assignmentLink.Id));
        Assert.True(await db.Groups.AnyAsync(group => group.Name == DemoSeed.MarkerGroupName));
    }

    [Fact]
    public async Task Seed_DoesNothing_WhenTheFlagIsOff()
    {
        using var database = new TestWebApplicationFactory();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var before = await CountAllAsync(db);

        await database.Services.SeedDemoDataAsync();

        Assert.Equal(before, await CountAllAsync(db));
        Assert.False(await db.Groups.AnyAsync(group => group.Name == DemoSeed.MarkerGroupName));
        Assert.Equal("Placeholder-skolan", (await db.Schools.SingleAsync(
            school => school.Id == Backend.Infrastructure.Persistence.Configurations.PlaceholderIds.SchoolId)).Name);
    }

    [Fact]
    public async Task Seed_NeverTouchesOtherSchoolsOrUsers()
    {
        using var database = new TestWebApplicationFactory();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var otherUser = await TestData.AddUserAsync(db);
        var otherGroup = await TestData.AddGroupAsync(db, otherUser.SchoolId);
        var otherHash = otherUser.PasswordHash;

        await scope.ServiceProvider.GetRequiredService<DemoSeed>().SeedAsync(CancellationToken.None);

        Assert.Equal(otherHash, (await db.Users.SingleAsync(user => user.Id == otherUser.Id)).PasswordHash);
        Assert.True(await db.Groups.AnyAsync(group => group.Id == otherGroup.Id));
        Assert.Equal("Edugrade", (await db.Schools.SingleAsync(
            school => school.Id == Backend.Infrastructure.Persistence.Configurations.PlaceholderIds.SchoolId)).Name);
        Assert.StartsWith("Skola ", (await db.Schools.SingleAsync(school => school.Id == otherUser.SchoolId)).Name);
    }

    private static WebApplicationFactory<Program> CreateApp(TestWebApplicationFactory database) =>
        database.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Database:SeedDemoData", "true");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(_demoDay));
            });
        });

    private static async Task<Counts> CountAllAsync(AppDb db) => new(
        await db.Users.CountAsync(),
        await db.Groups.CountAsync(),
        await db.Courses.CountAsync(),
        await db.Content.CountAsync(),
        await db.ContentLinks.CountAsync(),
        await db.Enrollments.CountAsync(),
        await db.Submissions.CountAsync(),
        await db.Grades.CountAsync(),
        await db.PartCompletions.CountAsync(),
        await db.Comments.CountAsync(),
        await db.Notifications.CountAsync());

    // Anropen görs som användaren, via testhandlern. Inloggningen med JWT prövas i AuthEndpointTests och
    // klockan i den här fixturen står på demodagen, vilket ett riktigt token inte skulle klara.
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

    private sealed record Counts(
        int Users, int Groups, int Courses, int Content, int Links, int Enrollments, int Submissions, int Grades,
        int Completions, int Comments, int Notifications);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
