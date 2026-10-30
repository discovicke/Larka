using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

/// <summary>
/// DemoContentRefresh: nytt katalogtext når en databas som redan har demodata, utan att röra inlämningar, datum
/// eller något en lärare lagt till. "Gammal prod" görs genom att seeda och sedan ändra databasen tillbaka.
/// </summary>
public class DemoContentRefreshTests
{
    private const string _extraPartTitle = "Min egen övning";

    [Fact]
    public async Task Refresh_OnFreshSeed_ChangesNothing_AndRecordsTheVersion()
    {
        using var database = new TestWebApplicationFactory();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await SeedAsync(scope);
        var before = await SnapshotAsync(db);
        var refresh = scope.ServiceProvider.GetRequiredService<DemoContentRefresh>();

        var first = await refresh.RefreshAsync(CancellationToken.None);
        var second = await refresh.RefreshAsync(CancellationToken.None);

        Assert.Equal(new DemoContentRefresh.Result(0, 0, 0), first);
        Assert.Null(second);
        Assert.Equal(before, await SnapshotAsync(db));
        Assert.Equal([DemoContentRefresh.PatchId], await db.DataPatches.Select(patch => patch.Id).ToListAsync());
    }

    [Fact]
    public async Task Refresh_WithoutDemoData_DoesNothing_AndRecordsNothing()
    {
        using var database = new TestWebApplicationFactory();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        var result = await scope.ServiceProvider.GetRequiredService<DemoContentRefresh>()
            .RefreshAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.False(await db.DataPatches.AnyAsync());
    }

    [Fact]
    public async Task SeedDemoData_RunsTheRefreshAfterTheSeed_AndRecordsTheVersion()
    {
        using var database = new TestWebApplicationFactory();
        using var app = database.WithWebHostBuilder(builder => builder.UseSetting("Database:SeedDemoData", "true"));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        await app.Services.SeedDemoDataAsync();

        Assert.True(await db.Groups.AnyAsync(group => group.Name == DemoSeed.MarkerGroupName));
        Assert.True(await db.DataPatches.AnyAsync(patch => patch.Id == DemoContentRefresh.PatchId));
    }

    [Fact]
    public async Task Refresh_OnOldProd_RestoresText_RecreatesMissingParts_AndLeavesEverythingElse()
    {
        using var database = new TestWebApplicationFactory();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await SeedAsync(scope);

        var group = await db.Groups.SingleAsync(row => row.Name == DemoSeed.MarkerGroupName);
        var courseSpec = DemoCatalog.Classes().Single(spec => spec.Name == ".NET25").Courses
            .Single(spec => spec.Code == ".NET25_DEVOPS");
        var course = await db.Courses.SingleAsync(row => row.Code == courseSpec.Code);
        var modules = await db.Modules.Where(row => row.CourseId == course.Id).OrderBy(row => row.Position)
            .ToListAsync();

        // Veckan där en del med inlämningar (inte den första delen) tas bort, och en annan får sin text ändrad.
        var moduleIndex = Array.FindIndex(
            courseSpec.Modules,
            module => module.Parts.Skip(1).Any(IsSubmittedPart));
        var moduleSpec = courseSpec.Modules[moduleIndex];
        var module = modules[moduleIndex];
        var deletedIndex = Array.FindIndex(
            moduleSpec.Parts, 1, IsSubmittedPart);
        var deletedTitle = moduleSpec.Parts[deletedIndex].Title;
        var editedTitle = moduleSpec.Parts[0].Title;

        var deletedContent = await ContentIn(db, group.Id, module.Id, deletedTitle).SingleAsync();

        // Inlämningarna hindrar att delen tas bort, så de går först (betygen följer med).
        await db.Submissions.Where(submission => db.ContentLinks.Any(
            link => link.Id == submission.ContentLinkId && link.ContentId == deletedContent.Id)).ExecuteDeleteAsync();
        await db.Content.Where(row => row.Id == deletedContent.Id).ExecuteDeleteAsync();

        var edited = await ContentIn(db, group.Id, module.Id, editedTitle).SingleAsync();
        edited.BodyMd = "En gammal text.";
        module.Description = "En gammal beskrivning.";
        course.Description = "En gammal kursbeskrivning.";

        var news = await db.Content.Where(row => row.Type == ContentType.News
                && db.Comments.Count(comment => comment.ContentId == row.Id) >= 2
                && db.ContentLinks.Any(link => link.ContentId == row.Id && link.GroupId == group.Id))
            .OrderBy(row => row.CreatedAt).FirstAsync();
        var originalNewsBody = news.BodyMd;
        news.BodyMd = "En gammal nyhet.";
        var removedComment = await db.Comments.Where(comment => comment.ContentId == news.Id)
            .OrderByDescending(comment => comment.CreatedAt).FirstAsync();
        db.Comments.Remove(removedComment);

        // En del en lärare lagt till själv, sist i veckan.
        var extra = new Content
        {
            AuthorId = news.AuthorId,
            Type = ContentType.Theory,
            Title = _extraPartTitle,
            BodyMd = "Min text."
        };
        db.Content.Add(extra);
        db.ContentLinks.Add(new ContentLink
        {
            ContentId = extra.Id,
            GroupId = group.Id,
            CourseId = course.Id,
            ModuleId = module.Id,
            Position = moduleSpec.Parts.Length + 1
        });
        await db.SaveChangesAsync();

        var before = await SnapshotAsync(db);
        var dueBefore = await db.ContentLinks.ToDictionaryAsync(link => link.Id, link => link.DueAt);
        var expectedBody = moduleSpec.Parts[0].Body;
        db.ChangeTracker.Clear();

        var result = await scope.ServiceProvider.GetRequiredService<DemoContentRefresh>()
            .RefreshAsync(CancellationToken.None);

        Assert.Equal(new DemoContentRefresh.Result(4, 1, 1), result);

        // Texten är tillbaka, och ingenting annat i veckan har ändrats.
        db.ChangeTracker.Clear();
        Assert.Equal(expectedBody, (await ContentIn(db, group.Id, module.Id, editedTitle).SingleAsync()).BodyMd);
        Assert.Equal(moduleSpec.Description, (await db.Modules.SingleAsync(row => row.Id == module.Id)).Description);
        var restored = await db.Courses.SingleAsync(row => row.Id == course.Id);
        Assert.Equal((courseSpec.Title, courseSpec.Description), (restored.Title, restored.Description));
        Assert.Equal(
            (course.Code, course.Points, course.StartsOn),
            (restored.Code, restored.Points, restored.StartsOn));
        Assert.Equal(originalNewsBody, (await db.Content.SingleAsync(row => row.Id == news.Id)).BodyMd);
        Assert.Equal(
            before.Comments + 1,
            await db.Comments.CountAsync());
        Assert.Equal(
            1,
            await db.Comments.CountAsync(comment => comment.ContentId == news.Id
                && comment.AuthorId == removedComment.AuthorId
                && comment.BodyMd == removedComment.BodyMd));

        // Delen som saknades är tillbaka på sin plats, utan inlämningar, och lärarens egen del ligger kvar sist.
        var recreated = await ContentIn(db, group.Id, module.Id, deletedTitle).SingleAsync();
        Assert.NotEqual(deletedContent.Id, recreated.Id);
        Assert.False(await db.Submissions.AnyAsync(submission => db.ContentLinks.Any(
            link => link.Id == submission.ContentLinkId && link.ContentId == recreated.Id)));
        var titles = await db.ContentLinks.Where(link => link.GroupId == group.Id && link.ModuleId == module.Id)
            .OrderBy(link => link.Position)
            .Select(link => db.Content.First(row => row.Id == link.ContentId).Title)
            .ToListAsync();
        Assert.Equal([.. moduleSpec.Parts.Select(part => part.Title), _extraPartTitle], titles);
        Assert.Equal("Min text.", (await db.Content.SingleAsync(row => row.Id == extra.Id)).BodyMd);

        // Inlämningar, betyg, klarmarkeringar och datum är orörda.
        var after = await SnapshotAsync(db);
        Assert.Equal(
            before with { Comments = before.Comments + 1, Content = before.Content + 1, Links = before.Links + 1 },
            after);
        var dueAfter = await db.ContentLinks.ToDictionaryAsync(link => link.Id, link => link.DueAt);
        Assert.All(dueBefore, pair => Assert.Equal(pair.Value, dueAfter[pair.Key]));
    }

    // Migrationen gjorde varje klasslärare till lärare i alla klassens kurser. Uppdateringen gör om det till katalogens
    // lärare per kurs, men rör inga konton som katalogen inte känner.
    [Fact]
    public async Task Refresh_NarrowsTheBackfilledCourseTeachers_ToTheCatalog_AndLeavesRealUsers()
    {
        using var database = new TestWebApplicationFactory();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await SeedAsync(scope);

        // Läget efter migrationen: varje lärare i en klass är lärare i alla klassens kurser.
        await db.CourseTeachers.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
        var backfilled = await db.Enrollments
            .Where(enrollment => enrollment.Role == GroupRole.Teacher)
            .Join(
                db.GroupCourses,
                enrollment => enrollment.GroupId,
                groupCourse => groupCourse.GroupId,
                (enrollment, groupCourse) => new { groupCourse.CourseId, enrollment.UserId })
            .Distinct()
            .ToListAsync();
        db.CourseTeachers.AddRange(backfilled.Select(row =>
            new CourseTeacher { CourseId = row.CourseId, UserId = row.UserId }));

        // En riktig lärare i .NET25, som inte finns i katalogen, med alla klassens kurser.
        var group = await db.Groups.SingleAsync(row => row.Name == DemoSeed.MarkerGroupName);
        var real = await TestData.AddUserAsync(db, group.SchoolId, UserRole.Teacher);
        db.Enrollments.Add(new Enrollment { UserId = real.Id, GroupId = group.Id, Role = GroupRole.Teacher });
        var classCourseIds = await db.GroupCourses.Where(link => link.GroupId == group.Id)
            .Select(link => link.CourseId).ToListAsync();
        db.CourseTeachers.AddRange(classCourseIds.Select(id => new CourseTeacher { CourseId = id, UserId = real.Id }));
        await db.SaveChangesAsync();

        // Det katalogen vill ha: personerna per kurskod.
        var emailsByCode = DemoCatalog.Classes().SelectMany(spec => spec.Courses)
            .ToDictionary(
                course => course.Code,
                course => course.Teachers.Select(key => key == "larare"
                        ? "larare"
                        : DemoPeople.Teachers.Concat(DemoPeople.Mentors).Single(person => person.Key == key).Email)
                    .Order().ToList());
        var catalogEmails = DemoPeople.Teachers.Concat(DemoPeople.Mentors).Select(person => person.Email)
            .Append("larare").ToHashSet();
        var emailOf = await db.Users.ToDictionaryAsync(user => user.Id, user => user.Email);
        var codeOf = await db.Courses.ToDictionaryAsync(course => course.Id, course => course.Code);
        var wanted = emailsByCode.SelectMany(pair => pair.Value.Select(email => (pair.Key, email))).ToHashSet();
        var have = backfilled.Select(row => (codeOf[row.CourseId], emailOf[row.UserId])).ToHashSet();
        var expectedRemoved = have.Count(pair => !wanted.Contains(pair));
        var expectedAdded = wanted.Count(pair => !have.Contains(pair));
        Assert.True(expectedRemoved > 0);
        Assert.True(expectedAdded > 0);
        db.ChangeTracker.Clear();

        var result = await scope.ServiceProvider.GetRequiredService<DemoContentRefresh>()
            .RefreshAsync(CancellationToken.None);

        Assert.Equal((expectedAdded, expectedRemoved), (result!.TeachersAdded, result.TeachersRemoved));
        var rows = (await db.CourseTeachers.ToListAsync())
            .Select(row => (codeOf[row.CourseId], emailOf.GetValueOrDefault(row.UserId) ?? "")).ToList();
        foreach (var (code, emails) in emailsByCode)
        {
            Assert.Equal(
                emails,
                rows.Where(row => row.Item1 == code && catalogEmails.Contains(row.Item2))
                    .Select(row => row.Item2).Order());
        }

        // Läraren som katalogen inte känner behåller alla kurser i klassen, och larare undervisar i DevOps och
        // Webbapplikationer men inte i Programmering med C#.
        Assert.Equal(
            classCourseIds.Order(),
            await db.CourseTeachers.Where(row => row.UserId == real.Id).Select(row => row.CourseId).OrderBy(id => id)
                .ToListAsync());
        Assert.Equal(
            [".NET25_DEVOPS", ".NET25_WEBB"],
            rows.Where(row => row.Item2 == "larare").Select(row => row.Item1).Order());
    }

    [Fact]
    public async Task Refresh_FindsRenamedItemsByWas_AndCreatesNewPartsInOrder()
    {
        using var database = new TestWebApplicationFactory();
        using var scope = database.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await SeedAsync(scope);
        var group = await db.Groups.SingleAsync(row => row.Name == DemoSeed.MarkerGroupName);

        var catalog = DemoCatalog.Classes();
        var classIndex = Array.FindIndex(catalog, spec => spec.Name == ".NET25");
        var classSpec = catalog[classIndex];
        var courseIndex = Array.FindIndex(classSpec.Courses, spec => spec.Code == ".NET25_DEVOPS");
        var courseSpec = classSpec.Courses[courseIndex];
        var moduleSpec = courseSpec.Modules[0];
        var oldPart = moduleSpec.Parts[0];
        var newsSpec = courseSpec.News.First(spec => spec.Comments.Length > 0);

        var extraPart = oldPart with { Title = "Ny övning", Body = "Ny text.", Was = null };
        var renamedModule = moduleSpec with
        {
            Title = "Vecka 1 – Nytt namn",
            Was = moduleSpec.Title,
            Parts =
            [
                oldPart with { Title = "Nytt namn", Body = "Ny brödtext.", Was = oldPart.Title },
                extraPart with { DueAt = null, Modes = [], Pattern = SubmissionPattern.None },
                .. moduleSpec.Parts.Skip(1)
            ]
        };
        var renamedNews = newsSpec with
        {
            Title = "Nytt nyhetsnamn",
            Was = newsSpec.Title,
            Comments = [.. newsSpec.Comments, new CommentSpec(newsSpec.Author, "Ny kommentar.", 30)]
        };
        var newsList = courseSpec.News.ToArray();
        newsList[Array.IndexOf(newsList, newsSpec)] = renamedNews;
        var courses = classSpec.Courses.ToArray();
        courses[courseIndex] = courseSpec with
        {
            Modules = [renamedModule, .. courseSpec.Modules.Skip(1)],
            News = newsList
        };
        catalog[classIndex] = classSpec with { Courses = courses };

        var course = await db.Courses.SingleAsync(row => row.Code == courseSpec.Code);
        var module = await db.Modules.SingleAsync(row => row.CourseId == course.Id && row.Title == moduleSpec.Title);
        var oldId = (await ContentIn(db, group.Id, module.Id, oldPart.Title).SingleAsync()).Id;
        var oldNewsId = (await db.Content.SingleAsync(row => row.Title == newsSpec.Title
            && db.ContentLinks.Any(link => link.ContentId == row.Id && link.GroupId == group.Id))).Id;
        var contentBefore = await db.Content.CountAsync();
        var modulesBefore = await db.Modules.CountAsync();
        db.ChangeTracker.Clear();

        var result = await scope.ServiceProvider.GetRequiredService<DemoContentRefresh>()
            .RefreshAsync(catalog, CancellationToken.None);

        // Veckan, delen och nyheten är samma rader som förut, bara omdöpta. Den nya delen är den enda nya raden.
        Assert.Equal(new DemoContentRefresh.Result(3, 1, 1), result);
        db.ChangeTracker.Clear();
        Assert.Equal(modulesBefore, await db.Modules.CountAsync());
        Assert.Equal(contentBefore + 1, await db.Content.CountAsync());
        Assert.Equal("Vecka 1 – Nytt namn", (await db.Modules.SingleAsync(row => row.Id == module.Id)).Title);
        var renamed = await db.Content.SingleAsync(row => row.Id == oldId);
        Assert.Equal(("Nytt namn", "Ny brödtext."), (renamed.Title, renamed.BodyMd));
        Assert.Equal("Nytt nyhetsnamn", (await db.Content.SingleAsync(row => row.Id == oldNewsId)).Title);
        Assert.Equal(1, await db.Comments.CountAsync(comment => comment.ContentId == oldNewsId
            && comment.BodyMd == "Ny kommentar."));

        // Den nya delen ligger som nummer två, och resten har flyttat ett steg.
        var titles = await db.ContentLinks.Where(link => link.GroupId == group.Id && link.ModuleId == module.Id)
            .OrderBy(link => link.Position)
            .Select(link => db.Content.First(row => row.Id == link.ContentId).Title)
            .ToListAsync();
        Assert.Equal(["Nytt namn", "Ny övning", .. moduleSpec.Parts.Skip(1).Select(part => part.Title)], titles);
        var positions = await db.ContentLinks.Where(link => link.GroupId == group.Id && link.ModuleId == module.Id)
            .Select(link => link.Position).OrderBy(position => position).ToListAsync();
        Assert.Equal(Enumerable.Range(1, titles.Count), positions);
    }

    private static bool IsSubmittedPart(PartSpec part) =>
        part.Modes.Length > 0 && part.Pattern != SubmissionPattern.None;

    private static async Task SeedAsync(IServiceScope scope) =>
        await scope.ServiceProvider.GetRequiredService<DemoSeed>().SeedAsync(CancellationToken.None);

    private static IQueryable<Content> ContentIn(AppDb db, Guid groupId, Guid moduleId, string title) =>
        db.Content.Where(row => row.Title == title
            && db.ContentLinks.Any(link => link.ContentId == row.Id && link.GroupId == groupId
                && link.ModuleId == moduleId));

    private static async Task<Counts> SnapshotAsync(AppDb db) => new(
        await db.Modules.CountAsync(),
        await db.Content.CountAsync(),
        await db.ContentLinks.CountAsync(),
        await db.Submissions.CountAsync(),
        await db.Grades.CountAsync(),
        await db.PartCompletions.CountAsync(),
        await db.Comments.CountAsync(),
        await db.ContentLinkDependencies.CountAsync());

    private sealed record Counts(
        int Modules, int Content, int Links, int Submissions, int Grades, int Completions, int Comments,
        int Dependencies);
}
