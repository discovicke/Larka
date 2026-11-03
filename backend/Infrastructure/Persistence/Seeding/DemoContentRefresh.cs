using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using static Backend.Infrastructure.Persistence.Seeding.DemoPeople;

namespace Backend.Infrastructure.Persistence.Seeding;

/// <summary>
/// För in ändringar i DemoCatalog i en databas som redan har demodata. DemoSeed hoppar över allt när klassen .NET25
/// finns, så utan det här når nytt innehåll aldrig prod. Körs vid start direkt efter DemoSeed, en gång per
/// <see cref="ContentVersion"/> (versionen sparas i DataPatches). Den som ändrar katalogen höjer versionen.
///
/// Rör bara det katalogen beskriver, och bara det som är text: veckors titel och beskrivning, delars titel och
/// brödtext, nyheters titel och brödtext. Saknas en vecka, del, nyhet eller kommentar skapas den som DemoSeed hade
/// gjort, men utan inlämningar. Datum, skalor, lägen, inlämningar, betyg, klarmarkeringar, checklistebockar och
/// allt en lärare lagt till själv lämnas orört, och inget raderas. Allt sparas i en transaktion.
///
/// Undantaget är kursernas lärare (CourseTeachers). Migrationen som införde dem gjorde varje lärare i en klass till
/// lärare i alla klassens kurser, och här görs det om till det katalogen beskriver: i varje demokurs tas lärare ur
/// katalogens personer bort om katalogen inte har dem där, och de som katalogen har men som saknas läggs till.
/// Andra användare än katalogens personer (riktiga konton) lämnas alltid orörda.
///
/// Det andra undantaget är betygsreglerna: en demouppgift vars elever ska komplettera eller har fått Ej inlämnat
/// får samma kompletteringsdeadline som DemoSeed ger den (<see cref="DemoBuilder.ResubmitFor"/>), men bara om
/// uppgiften saknar en. Ett IG på en uppgift där en deadline återstår blir Komplettera igen. Inget annat ändras.
/// </summary>
public sealed class DemoContentRefresh(AppDb db, TimeProvider time, ILogger<DemoContentRefresh> logger)
{
    /// <summary>Höj när DemoCatalog ändrats så att prod ska få ändringen. Format: ÅÅÅÅ-MM-DD.löpnummer.</summary>
    public const string ContentVersion = "2026-11-03.1";

    internal static string PatchId => $"demo-content@{ContentVersion}";

    private readonly Dictionary<string, User> _users = [];
    private readonly Dictionary<Guid, GroupData> _groups = [];
    private readonly Dictionary<Guid, List<Module?>> _modules = [];
    private readonly HashSet<Guid> _matched = [];
    private int _updated;
    private int _created;
    private int _commentsAdded;
    private int _teachersAdded;
    private int _teachersRemoved;
    private int _deadlinesAdded;
    private int _failsReopened;
    private readonly List<(ContentLink Link, PartSpec Part)> _deadlineCandidates = [];

    /// <summary>Resultatet av en körning. Null om versionen redan var körd eller det inte finns någon demodata.</summary>
    internal sealed record Result(
        int Updated, int Created, int CommentsAdded, int TeachersAdded = 0, int TeachersRemoved = 0,
        int DeadlinesAdded = 0, int FailsReopened = 0);

    internal Task<Result?> RefreshAsync(CancellationToken ct) => RefreshAsync(DemoCatalog.Classes(), ct);

    internal async Task<Result?> RefreshAsync(ClassSpec[] catalog, CancellationToken ct)
    {
        if (await db.DataPatches.AnyAsync(patch => patch.Id == PatchId, ct))
        {
            logger.LogInformation("Demoinnehållet är redan uppdaterat till {Version}.", ContentVersion);
            return null;
        }

        var school = await DemoSeed.FindSchoolAsync(db, ct);
        if (school is null
            || !await db.Groups.AnyAsync(
                group => group.SchoolId == school.Id && group.Name == DemoSeed.MarkerGroupName, ct))
        {
            logger.LogInformation("Demoinnehållet uppdaterades inte: det finns ingen demodata.");
            return null;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await LoadUsersAsync(ct);
        var groups = await db.Groups.Where(group => group.SchoolId == school.Id).ToListAsync(ct);
        var courses = await db.Courses.Where(course => course.SchoolId == school.Id).ToListAsync(ct);
        var linked = (await db.GroupCourses.Select(link => new { link.GroupId, link.CourseId }).ToListAsync(ct))
            .Select(link => (link.GroupId, link.CourseId))
            .ToHashSet();

        foreach (var classSpec in catalog)
        {
            var group = groups.FirstOrDefault(row => row.Name == classSpec.Name);
            if (group is null)
            {
                continue;
            }

            var data = await LoadGroupAsync(group.Id, ct);
            var students = StudentsOf(classSpec);
            foreach (var courseSpec in classSpec.Courses)
            {
                var course = courses.FirstOrDefault(row => row.Code == courseSpec.Code);
                if (course is not null && linked.Contains((group.Id, course.Id)))
                {
                    await RefreshCourseAsync(group, data, course, courseSpec, students, ct);
                    await RefreshTeachersAsync(course, courseSpec, ct);
                }
            }

            RefreshNews(group, data, null, classSpec.News, students);
        }

        await ApplyGradingRulesAsync(ct);

        db.DataPatches.Add(new DataPatch { Id = PatchId, AppliedAt = time.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Demoinnehållet uppdaterat till {Version}: {Updated} ändrade, {Created} nya och {Comments} nya kommentarer, "
            + "{TeachersAdded} lärare tillagda och {TeachersRemoved} borttagna i kurserna, "
            + "{Deadlines} kompletteringsdeadliner och {Fails} IG som blev Komplettera.",
            ContentVersion,
            _updated,
            _created,
            _commentsAdded,
            _teachersAdded,
            _teachersRemoved,
            _deadlinesAdded,
            _failsReopened);
        return new Result(
            _updated, _created, _commentsAdded, _teachersAdded, _teachersRemoved, _deadlinesAdded, _failsReopened);
    }

    // Låter en befintlig demo följa betygsreglerna. Bara delar som katalogen matchat rörs, alltså demoklassernas egna
    // länkar. En länk med huvuddeadline men utan kompletteringsdeadline får den som DemoSeed ger, om någon elev ligger
    // på Komplettera eller Ej inlämnat. Ett IG på en länk där en deadline återstår blir Komplettera, för IG sätts
    // först när ingen deadline återstår. Körs bara en gång per version, och ger inget nytt andra gången ändå.
    private async Task ApplyGradingRulesAsync(CancellationToken ct)
    {
        var links = _deadlineCandidates.Where(item => item.Link.DueAt is not null).ToList();
        var linkIds = links.Select(item => item.Link.Id).ToList();
        var submissions = await db.Submissions
            .Include(row => row.Grade)
            .Where(row => linkIds.Contains(row.ContentLinkId))
            .ToListAsync(ct);
        var now = time.GetUtcNow();

        foreach (var (link, part) in links)
        {
            var rows = submissions.Where(row => row.ContentLinkId == link.Id).ToList();
            if (link.ResubmitDueAt.Count == 0
                && rows.Any(row => row.Status is SubmissionStatus.NeedsWork or SubmissionStatus.NotSubmitted))
            {
                var wanted = DemoBuilder.ResubmitFor(part with { DueAt = link.DueAt });
                if (wanted.Count > 0)
                {
                    link.ResubmitDueAt = wanted;
                    _deadlinesAdded++;
                }
            }

            if (!new GradingWindow(link.DueAt, link.ResubmitDueAt, now).DeadlineRemains)
            {
                continue;
            }

            foreach (var row in rows.Where(row => row.Status == SubmissionStatus.Graded
                && row.Grade?.Level == GradeLevel.Fail))
            {
                row.Status = SubmissionStatus.NeedsWork;
                row.Grade!.Level = null;
                _failsReopened++;
            }
        }
    }

    // Katalogens lärare i kursen. Bara katalogens personer rörs: en lärare som katalogen inte har i kursen tas bort,
    // och en som katalogen har men som saknas läggs till. Ett riktigt konto, som inte finns i katalogen, lämnas.
    private async Task RefreshTeachersAsync(Course course, CourseSpec spec, CancellationToken ct)
    {
        var wanted = spec.Teachers
            .Select(UserOf)
            .OfType<User>()
            .Select(user => user.Id)
            .ToHashSet();
        var catalogPeople = Teachers.Concat(Mentors).Select(person => person.Email).Append("larare").ToList();
        var catalogUserIds = _users.Values
            .Where(user => catalogPeople.Contains(user.Email))
            .Select(user => user.Id)
            .ToHashSet();

        var existing = await db.CourseTeachers.Where(row => row.CourseId == course.Id).ToListAsync(ct);
        foreach (var row in existing.Where(row => catalogUserIds.Contains(row.UserId) && !wanted.Contains(row.UserId)))
        {
            db.CourseTeachers.Remove(row);
            _teachersRemoved++;
        }

        foreach (var userId in wanted.Where(id => existing.All(row => row.UserId != id)))
        {
            db.CourseTeachers.Add(new CourseTeacher { CourseId = course.Id, UserId = userId });
            _teachersAdded++;
        }
    }

    private async Task RefreshCourseAsync(
        Group group, GroupData data, Course course, CourseSpec spec, User?[] students, CancellationToken ct)
    {
        // Veckorna hör till kursen och delas av alla klasser som läser den, så de tas bara en gång.
        if (!_modules.TryGetValue(course.Id, out var modules))
        {
            if (course.Title != spec.Title || course.Description != spec.Description)
            {
                course.Title = spec.Title;
                course.Description = spec.Description;
                _updated++;
            }

            modules = await RefreshModulesAsync(course, spec, ct);
            _modules[course.Id] = modules;
        }

        var author = UserOf(spec.Author);
        for (var index = 0; index < spec.Modules.Length; index++)
        {
            var module = modules[index];
            if (module is not null)
            {
                RefreshParts(group, data, course, spec, spec.Modules[index], module, author);
            }
        }

        RefreshNews(group, data, course, spec.News, students);
    }

    private async Task<List<Module?>> RefreshModulesAsync(Course course, CourseSpec spec, CancellationToken ct)
    {
        var existing = await db.Modules.Where(row => row.CourseId == course.Id).ToListAsync(ct);
        var ordered = existing.OrderBy(row => row.Position).ToList();
        var resolved = new List<Module?>();
        var matched = new HashSet<Guid>();

        foreach (var moduleSpec in spec.Modules)
        {
            var module = FindUnmatched(
                ordered, row => row.Title, row => row.Id, matched, moduleSpec.Title, moduleSpec.Was);
            if (module is null)
            {
                module = DemoBuilder.NewModule(course.Id, moduleSpec, 0);
                db.Modules.Add(module);

                // In efter veckan som föregår den i katalogen, så att ordningen blir densamma som i en ny seed.
                var previous = resolved.Count > 0
                    ? resolved[^1]
                    : null;
                ordered.Insert(previous is null
                    ? 0
                    : ordered.IndexOf(previous) + 1, module);
                _created++;
            }
            else if (module.Title != moduleSpec.Title || module.Description != moduleSpec.Description)
            {
                module.Title = moduleSpec.Title;
                module.Description = moduleSpec.Description;
                _updated++;
            }

            matched.Add(module.Id);
            resolved.Add(module);
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Position != index + 1)
            {
                ordered[index].Position = index + 1;
            }
        }

        return resolved;
    }

    private void RefreshParts(
        Group group, GroupData data, Course course, CourseSpec courseSpec, ModuleSpec spec, Module module, User? author)
    {
        var ordered = data.Links
            .Where(link => link.ModuleId == module.Id)
            .OrderBy(link => link.Position)
            .ToList();
        var matched = new HashSet<Guid>();
        var resolved = new List<(PartSpec Spec, ContentLink Link)>();

        foreach (var part in spec.Parts)
        {
            var link = FindUnmatched(
                ordered, row => data.Contents[row.ContentId].Title, row => row.Id, matched, part.Title, part.Was);
            if (link is not null)
            {
                _deadlineCandidates.Add((link, part));
                var content = data.Contents[link.ContentId];
                if (content.Title != part.Title || content.BodyMd != part.Body)
                {
                    content.Title = part.Title;
                    content.BodyMd = part.Body;
                    _updated++;
                }
            }
            else if (author is not null)
            {
                var publishAt = ordered.Count > 0
                    ? ordered[0].PublishAt
                    : DemoBuilder.PartPublishAt(courseSpec, spec);
                var (content, newLink) = DemoBuilder.NewPart(
                    part, author.Id, group.Id, course.Id, module.Id, 0, publishAt);
                db.Content.Add(content);
                db.ContentLinks.Add(newLink);
                data.Contents[content.Id] = content;
                data.Links.Add(newLink);

                var previous = resolved.Count > 0
                    ? resolved[^1].Link
                    : null;
                ordered.Insert(previous is null
                    ? 0
                    : ordered.IndexOf(previous) + 1, newLink);
                if (spec.Sequential && part.Type == ContentType.Theory)
                {
                    var blocking = resolved.LastOrDefault(item => item.Spec.Type == ContentType.Theory).Link;
                    if (blocking is not null)
                    {
                        db.ContentLinkDependencies.Add(new ContentLinkDependency
                        {
                            BlockedLinkId = newLink.Id,
                            BlockingLinkId = blocking.Id
                        });
                    }
                }

                link = newLink;
                _created++;
            }
            else
            {
                continue;
            }

            matched.Add(link.Id);
            resolved.Add((part, link));
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            if (ordered[index].Position != index + 1)
            {
                ordered[index].Position = index + 1;
            }
        }
    }

    private void RefreshNews(Group group, GroupData data, Course? course, NewsSpec[] specs, User?[] students)
    {
        foreach (var spec in specs)
        {
            var author = UserOf(spec.Author);
            var scope = spec.InCourse
                ? course?.Id
                : null;
            var candidates = data.Links
                .Where(link => link.ModuleId is null
                    && link.CourseId == scope
                    && data.Contents[link.ContentId].Type == ContentType.News)
                .ToList();
            var link = FindUnmatched(
                candidates, row => data.Contents[row.ContentId].Title, row => row.Id, _matched, spec.Title, spec.Was);
            Content content;
            if (link is not null)
            {
                content = data.Contents[link.ContentId];
                if (content.Title != spec.Title || content.BodyMd != spec.Body)
                {
                    content.Title = spec.Title;
                    content.BodyMd = spec.Body;
                    _updated++;
                }
            }
            else if (author is not null)
            {
                (content, link) = DemoBuilder.NewNews(spec, author.Id, group.Id, course?.Id);
                db.Content.Add(content);
                db.ContentLinks.Add(link);
                data.Contents[content.Id] = content;
                data.Links.Add(link);
                _created++;
            }
            else
            {
                continue;
            }

            _matched.Add(link.Id);
            AddMissingComments(data, content, spec, students);
        }
    }

    // Samma författare och text räknas som samma kommentar. Befintliga kommentarer ändras eller tas aldrig bort.
    private void AddMissingComments(GroupData data, Content content, NewsSpec spec, User?[] students)
    {
        data.Comments.TryGetValue(content.Id, out var existing);
        existing ??= [];
        data.Comments[content.Id] = existing;

        foreach (var comment in spec.Comments)
        {
            var author = comment.Author.StartsWith('#')
                ? students.ElementAtOrDefault(int.Parse(comment.Author[1..]))
                : UserOf(comment.Author);
            if (author is null || existing.Any(row => row.AuthorId == author.Id && row.BodyMd == comment.Body))
            {
                continue;
            }

            var added = DemoBuilder.NewComment(content.Id, author.Id, spec, comment);
            db.Comments.Add(added);
            existing.Add(added);
            _commentsAdded++;
        }
    }

    // Titeln först, sedan den gamla titeln. Rader som redan matchats en gång används inte igen.
    private static T? FindUnmatched<T>(
        List<T> rows, Func<T, string> title, Func<T, Guid> id, HashSet<Guid> matched, string current, string? was)
        where T : class =>
        rows.FirstOrDefault(row => !matched.Contains(id(row)) && title(row) == current)
        ?? (was is null
            ? null
            : rows.FirstOrDefault(row => !matched.Contains(id(row)) && title(row) == was));

    private async Task<GroupData> LoadGroupAsync(Guid groupId, CancellationToken ct)
    {
        if (_groups.TryGetValue(groupId, out var known))
        {
            return known;
        }

        var links = await db.ContentLinks.Where(link => link.GroupId == groupId).ToListAsync(ct);
        var contents = await db.Content
            .Where(content => db.ContentLinks.Any(link => link.GroupId == groupId && link.ContentId == content.Id))
            .ToDictionaryAsync(content => content.Id, ct);
        var newsIds = contents.Values.Where(content => content.Type == ContentType.News).Select(content => content.Id)
            .ToList();
        var comments = (await db.Comments.Where(comment => newsIds.Contains(comment.ContentId)).ToListAsync(ct))
            .GroupBy(comment => comment.ContentId)
            .ToDictionary(rows => rows.Key, rows => rows.ToList());

        var data = new GroupData(links, contents, comments);
        _groups[groupId] = data;
        return data;
    }

    private async Task LoadUsersAsync(CancellationToken ct)
    {
        var emails = Teachers
            .Concat(Mentors)
            .Concat(Enumerable.Range(0, 80).Select(Student))
            .Select(person => person.Email)
            .Append("larare")
            .Append("elev")
            .ToList();
        foreach (var user in await db.Users.Where(row => emails.Contains(row.Email)).ToListAsync(ct))
        {
            _users[user.Email] = user;
        }
    }

    /// <summary>Användaren för en nyckel (lärare, mentor, elevN, larare eller elev). Skapas aldrig här.</summary>
    private User? UserOf(string key)
    {
        var email = key is "larare" or "elev"
            ? key
            : (Teachers.Concat(Mentors).FirstOrDefault(person => person.Key == key)
                ?? Student(int.Parse(key["elev".Length..]))).Email;
        return _users.GetValueOrDefault(email);
    }

    // Klassens elever i samma ordning som DemoBuilder, så att #N pekar på samma elev. Saknad elev blir null.
    private User?[] StudentsOf(ClassSpec spec)
    {
        var students = Enumerable.Range(spec.FirstStudent, spec.StudentCount)
            .Select(index => _users.GetValueOrDefault(Student(index).Email))
            .ToList();
        if (spec.HasPublicStudent)
        {
            students.Insert(Math.Min(DemoBuilder.PublicStudentPosition, students.Count), UserOf("elev"));
        }

        return [.. students];
    }

    private sealed record GroupData(
        List<ContentLink> Links, Dictionary<Guid, Content> Contents, Dictionary<Guid, List<Comment>> Comments);
}
