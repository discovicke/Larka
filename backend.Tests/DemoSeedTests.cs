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
            : this(null)
        {
        }

        // templateRepo sätter Database:DemoTemplateRepo, så att demodatan byggs med GitHub-uppgifter från start.
        internal DemoFixture(string? templateRepo)
        {
            Factory = _database.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:SeedDemoData", "true");
                if (templateRepo is not null)
                {
                    builder.UseSetting("Database:DemoTemplateRepo", templateRepo);
                }

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(_demoDay));

                    // Som Supabase: strömmen är stängd när uppladdningen är klar.
                    services.RemoveAll<IFileStorage>();
                    services.AddSingleton<IFileStorage>(new ClosingFileStorage());
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
            Assert.True(row.Submission.SubmittedAt is null || row.Submission.SubmittedAt <= row.Grade.GradedAt);
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
    public async Task WithoutGitHub_ProgrammingAssignmentsTakeTextAndFiles_AndOtherModesMatchTheWork()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var specs = await SpecsByTitleAsync(db);

        Assert.Equal(
            [SubmissionMode.Text, SubmissionMode.File],
            specs[(".NET25_DevOps", "Inlämning 4: Driftsätt appen")].Modes);
        Assert.Equal(
            [SubmissionMode.Text, SubmissionMode.File],
            specs[(".NET25_WeApp", "Inlämning 1: Minimal API för en bokhylla")].Modes);
        Assert.Contains("zip-fil", specs[(".NET25_WeApp", "Inlämning 1: Minimal API för en bokhylla")].Body);
        Assert.Equal(
            [SubmissionMode.File, SubmissionMode.Text],
            specs[("FIBER25_NaUp", "Inlämning 1: Rita nätet för kvarteret")].Modes);
        Assert.Equal([SubmissionMode.OnSite], specs[("FIBER25_FiTe", _practicalTest)].Modes);
        Assert.Equal(GradeScale.PassFail, specs[("FIBER25_FiTe", _practicalTest)].Scale);
        Assert.Equal(GradeScale.None, specs[("FIBER25_NaUp", _exercise)].Scale);
        Assert.Equal([SubmissionMode.Text], specs[("FIBER25_FiTe", "Inlämning 1: Dämpningsbudget för en länk")].Modes);
        Assert.False(await db.StudentRepos.AnyAsync());
        Assert.False(await db.AssignmentGroups.AnyAsync());

        // Bedömning på plats har inga inlämningar från eleven, och övningen har feedback utan betyg.
        var onSite = await SubmissionsForAsync(db, "FIBER25_FiTe", _practicalTest);
        Assert.All(onSite, row => Assert.Null(row.Submission.SubmittedAt));
        Assert.Contains(onSite, row => row.Grade?.Level is GradeLevel.Pass or GradeLevel.Fail);
        Assert.DoesNotContain(onSite, row => row.Grade?.Level == GradeLevel.Distinction);
        var exercise = await SubmissionsForAsync(db, "FIBER25_NaUp", _exercise);
        Assert.Contains(exercise, row => row.Grade is not null);
        Assert.All(exercise.Where(row => row.Grade is not null), row => Assert.Null(row.Grade!.Level));
    }

    [Fact]
    public async Task Seed_AttachesTheFilesThatTheAssignmentsTalkAbout_AndStudentFiles()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var attachments = await (
                from attachment in db.Attachments
                join content in db.Content on attachment.ContentId equals content.Id
                select new { content.Title, attachment.FileName, attachment.StorageKey, attachment.SizeBytes })
            .ToListAsync();
        Assert.Contains(attachments, row =>
            row.Title == "Inlämning 2: Tolka en OTDR-kurva" && row.FileName == "otdr-lank-a.png");
        Assert.Contains(attachments, row => row.FileName == "karta-kvarteret.png");
        Assert.Contains(attachments, row => row.FileName == "installationsprotokoll-mall.pdf");
        Assert.All(attachments, row => Assert.True(row.StorageKey.Length > 0 && row.SizeBytes > 1000));

        // Varje uppgift som nämner en bilaga har en.
        var mentions = await db.Content
            .Where(content => content.Type == ContentType.Assignment && content.BodyMd.Contains("bilaga"))
            .Select(content => content.Id)
            .ToListAsync();
        Assert.NotEmpty(mentions);
        foreach (var id in mentions)
        {
            Assert.True(await db.Attachments.AnyAsync(row => row.ContentId == id));
        }

        var files = await (
                from file in db.SubmissionFiles
                join submission in db.Submissions on file.SubmissionId equals submission.Id
                select new { file.FileName, file.StorageKey, submission.SubmittedAt })
            .ToListAsync();
        Assert.Contains(files, row => row.FileName == "natskiss-kvarteret.png");
        Assert.All(files, row => Assert.NotNull(row.SubmittedAt));
        Assert.Equal(files.Count, files.Select(row => row.StorageKey).Distinct().Count());
        var url = await storage.GetSignedUrlAsync(files[0].StorageKey, IFileStorage.SignedUrlLifetime, default);
        Assert.False(string.IsNullOrEmpty(url));
    }

    [Fact]
    public async Task Seed_GivesTheFiberStudentADemoPathWithAFileAssignment()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var student = await db.Users.SingleAsync(user => user.Email == "elev-fiber");
        var fiber = await db.Groups.SingleAsync(group => group.Name == "FIBER25");
        Assert.True(await db.Enrollments.AnyAsync(row =>
            row.GroupId == fiber.Id && row.UserId == student.Id && row.Role == GroupRole.Student));
        Assert.False(await db.Enrollments.AnyAsync(row =>
            row.GroupId == DemoSeed.MarkerGroupId && row.UserId == student.Id));

        var course = await db.Courses.SingleAsync(row => row.Code == "FIBER25_NaUp");
        var open = await (
                from link in db.ContentLinks
                join content in db.Content.Include(row => row.Assignment) on link.ContentId equals content.Id
                where link.CourseId == course.Id && link.PublishAt <= _demoDay && link.DueAt > _demoDay
                select new { Link = link, Content = content })
            .SingleAsync();
        Assert.Contains(SubmissionMode.File, open.Content.Assignment!.SubmissionModes);
        Assert.True(open.Link.DueAt > _demoDay.AddDays(7));
        Assert.True(await db.StudentHelpRequests.AnyAsync(request =>
            request.ContentLinkId == open.Link.Id && request.StudentId != student.Id));
    }

    [Fact]
    public async Task WithATemplate_ProgrammingAssignmentsBecomeRepoAssignments_WithGroupReposForGroupProjects()
    {
        using var github = new DemoFixture("skolan/mall");
        using var scope = github.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        await AssertRepoAssignmentsAsync(db, "skolan/mall");

        // Innan deadline har ingen lämnat in i repo-läge.
        var open = await SubmissionsForAsync(db, ".NET25_DevOps", "Inlämning 4: Driftsätt appen");
        Assert.All(open, row => Assert.Equal(SubmissionStatus.NotStarted, row.Submission.Status));

        // En projektgrupp har samma läge och betyg, och demoeleven är i första gruppen.
        var elev = await db.Users.SingleAsync(user => user.Email == "elev");
        foreach (var title in new[] { "Inlämning 1: Skydda main-grenen", "Inlämning 3: Pipeline för pull requests" })
        {
            var rows = await SubmissionsForAsync(db, ".NET25_DevOps", title);
            Assert.All(rows.GroupBy(row => row.Submission.AssignmentGroupId), group =>
                Assert.Single(group.Select(row => (row.Submission.Status, row.Grade?.Level)).Distinct()));
            var groupId = rows.Single(row => row.Submission.StudentId == elev.Id).Submission.AssignmentGroupId;
            var first = await db.AssignmentGroups.SingleAsync(group => group.Id == groupId);
            Assert.Equal("Grupp 1", first.Name);
        }
    }

    [Fact]
    public async Task ConnectedGitHub_TurnsTheProgrammingAssignmentsIntoRepoAssignments_Once()
    {
        using var later = new DemoFixture();
        using var scope = later.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var seed = scope.ServiceProvider.GetRequiredService<DemoSeed>();
        var github = (TestGitHubAppService)scope.ServiceProvider.GetRequiredService<IGitHubAppService>();

        // Utan kopplad GitHub förblir uppgifterna text och filer.
        Assert.Equal(0, await seed.EnableGitHubAssignmentsAsync(CancellationToken.None));

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

        var created = await seed.EnableGitHubAssignmentsAsync(CancellationToken.None);
        var again = await seed.EnableGitHubAssignmentsAsync(CancellationToken.None);

        db.ChangeTracker.Clear();
        Assert.Equal(_groupRepos, created);
        Assert.Equal(0, again);
        await AssertRepoAssignmentsAsync(db, "skolan/drifts-mall");
    }

    private const string _practicalTest = "Praktiskt prov: Svetsskarva och mät";
    private const string _exercise = "Övning: Hur mycket rör går åt?";

    // Bara DevOps har GitHub: fem uppgifter med två projektgrupper var.
    private const int _groupRepos = 5 * 2;

    private static async Task AssertRepoAssignmentsAsync(AppDb db, string template)
    {
        var specs = await SpecsByTitleAsync(db);
        var devops = specs[(".NET25_DevOps", "Inlämning 4: Driftsätt appen")];
        Assert.Equal([SubmissionMode.Repo, SubmissionMode.Text], devops.Modes);
        Assert.Equal(RepoWorkMode.Group, devops.WorkMode);
        Assert.Equal(template, devops.Template);
        Assert.Contains("GitHub-repo", devops.Body);
        Assert.DoesNotContain("zip-fil", devops.Body);

        // Övrig kod lämnas in som zip-fil och text, även när GitHub finns.
        var webApp = specs[(".NET25_WeApp", "Inlämning 1: Minimal API för en bokhylla")];
        Assert.Equal([SubmissionMode.Text, SubmissionMode.File], webApp.Modes);
        Assert.Contains("zip-fil", webApp.Body);
        Assert.Equal(
            ["DevOps"],
            specs.Where(pair => pair.Value.Modes.Contains(SubmissionMode.Repo))
                .Select(pair => pair.Key.Code.Split('_')[1])
                .Distinct());
        foreach (var spec in specs.Values.Where(spec => spec.Modes.Contains(SubmissionMode.Repo)))
        {
            Assert.Null(GitHubRepoNaming.Validate(spec.Pattern, spec.WorkMode));
        }

        // Fiberklassen har inga programmeringsuppgifter och rörs inte.
        Assert.Equal(
            [SubmissionMode.File, SubmissionMode.Text],
            specs[("FIBER25_NaUp", "Inlämning 1: Rita nätet för kvarteret")].Modes);
        Assert.DoesNotContain(specs, pair =>
            pair.Key.Code.StartsWith("FIBER25", StringComparison.Ordinal)
            && pair.Value.Modes.Contains(SubmissionMode.Repo));

        var repos = await db.StudentRepos.ToListAsync();
        Assert.Equal(_groupRepos, repos.Count);
        Assert.All(repos, repo => Assert.NotNull(repo.AssignmentGroupId));
        Assert.All(repos, repo => Assert.Equal(RepoProvisioningState.Pending, repo.ProvisioningState));
        Assert.Equal(repos.Count, repos.Select(repo => repo.DesiredRepoName).Distinct().Count());
        Assert.Contains(repos, repo => repo.DesiredRepoName == "devops-inl-mning-4-drifts-tt-appen-grupp-1");
        Assert.All(repos, repo => Assert.Matches("^[a-z0-9-]+$", repo.DesiredRepoName!));

        // Varje elev i en grupp-uppgift hör till en av två grupper med fyra medlemmar.
        var groups = await db.AssignmentGroups.Include(group => group.Members).ToListAsync();
        Assert.Equal(_groupRepos, groups.Count);
        Assert.All(groups, group => Assert.Equal(4, group.Members.Count));
        var devopsSubmissions = await SubmissionsForAsync(db, ".NET25_DevOps", "Inlämning 4: Driftsätt appen");
        Assert.Equal(8, devopsSubmissions.Count);
        Assert.All(devopsSubmissions, row => Assert.NotNull(row.Submission.AssignmentGroupId));
    }

    private sealed record SpecRow(
        List<SubmissionMode> Modes,
        GradeScale Scale,
        RepoWorkMode WorkMode,
        string? Template,
        string? Pattern,
        string Body);

    private static async Task<Dictionary<(string Code, string Title), SpecRow>> SpecsByTitleAsync(AppDb db)
    {
        var rows = await (
                from link in db.ContentLinks
                join course in db.Courses on link.CourseId equals course.Id
                join content in db.Content on link.ContentId equals content.Id
                join spec in db.AssignmentSpecs on content.Id equals spec.ContentId
                select new { course.Code, content.Title, content.BodyMd, Spec = spec })
            .ToListAsync();
        return rows.ToDictionary(
            row => (row.Code, row.Title),
            row => new SpecRow(
                row.Spec.SubmissionModes,
                row.Spec.GradeScale,
                row.Spec.WorkMode,
                row.Spec.TemplateRepo,
                row.Spec.RepoNamePattern,
                row.BodyMd));
    }

    private sealed record SubmissionRow(
        Backend.Domain.Entities.Submission Submission,
        Backend.Domain.Entities.Grade? Grade);

    private static async Task<List<SubmissionRow>> SubmissionsForAsync(AppDb db, string code, string title)
    {
        var link = await (
                from row in db.ContentLinks
                join course in db.Courses on row.CourseId equals course.Id
                join content in db.Content on row.ContentId equals content.Id
                where course.Code == code && content.Title == title
                select row)
            .SingleAsync();
        var submissions = await db.Submissions.Where(row => row.ContentLinkId == link.Id).ToListAsync();
        var ids = submissions.Select(row => row.Id).ToList();
        var grades = await db.Grades.Where(grade => ids.Contains(grade.SubmissionId)).ToListAsync();
        return submissions
            .Select(row => new SubmissionRow(row, grades.SingleOrDefault(grade => grade.SubmissionId == row.Id)))
            .ToList();
    }

    [Fact]
    public async Task DemoAccounts_CanReadTheOverviewThatThePresentationUses()
    {
        var admin = await GetAsync<OverviewResponse>("admin", "/api/me/admin-overview");
        Assert.Equal([".NET25", "FIBER25", "JAVA25"], admin.ClassTimelines.Select(item => item.Name).Order());

        var student = await GetAsync<StudyPlanResponse>("elev", "/api/me/study-plan");
        Assert.Contains(student.Courses, course => course.Code == ".NET25_DevOps");
        var fiberStudent = await GetAsync<StudyPlanResponse>("elev-fiber", "/api/me/study-plan");
        Assert.Contains(fiberStudent.Courses, course => course.Code == "FIBER25_NaUp");
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

    private sealed class ClosingFileStorage : Backend.Infrastructure.Storage.InMemoryFileStorage, IFileStorage
    {
        async Task<string> IFileStorage.UploadAsync(
            string folder, Stream content, string contentType, CancellationToken ct)
        {
            var key = await UploadAsync(folder, content, contentType, ct);
            await content.DisposeAsync();
            return key;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
