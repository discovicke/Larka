using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Backend.Infrastructure.Persistence.Seeding.DemoPeople;

namespace Backend.Infrastructure.Persistence.Seeding;

/// <summary>
/// Realistisk demodata för Edugrade: tre klasser och ett klassråd med lärare, mentor och ett tjugotal elever
/// vardera, avslutade, pågående och kommande kurser, veckor med material och uppgifter, inlämningar i alla
/// lägen, betyg, nyheter och kommentarer. Körs vid start efter AccountSeed, bara när Database:SeedDemoData är
/// true (prod via appsettings.Production.json). Se DemoCatalog för innehållet och DemoPeople för namnen.
///
/// Additiv och idempotent: finns klassen .NET25 (markören) görs ingenting, och inget som seeden inte skapat själv
/// ändras eller tas bort. Undantagen är skolans namn (Placeholder-skolan blir Edugrade), demokontona larare och
/// elev som placeras i .NET25, och Demo-klass med Demo-kurs som tas bort om ingen lämnat in något där.
/// Allt sparas i en transaktion, så ett fel lämnar databasen som den var.
/// </summary>
public sealed class DemoSeed(AppDb db, IPasswordHasher<User> passwordHasher, ILogger<DemoSeed> logger)
{
    /// <summary>Gemensamt lösenord för alla seedade användare. Demodata är offentlig, så det är inte en hemlighet.</summary>
    public const string DemoPassword = "larka-demo";

    /// <summary>Klassen som visar att seeden redan har körts.</summary>
    public const string MarkerGroupName = ".NET25";

    public const string SchoolName = "Edugrade";
    private const string _placeholderSchoolName = "Placeholder-skolan";

    public async Task<bool> SeedAsync(CancellationToken ct)
    {
        var school = await FindSchoolAsync(ct);
        if (school is null)
        {
            logger.LogWarning("Demodata hoppades över: det finns ingen skola.");
            return false;
        }

        if (await db.Groups.AnyAsync(group => group.SchoolId == school.Id && group.Name == MarkerGroupName, ct))
        {
            logger.LogInformation("Demodata finns redan i skolan {School}, inget lades till.", school.Name);
            return false;
        }

        var publicTeacher = await FindAccountAsync("larare", ct);
        var publicStudent = await FindAccountAsync("elev", ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (school.Name == _placeholderSchoolName)
        {
            school.Name = SchoolName;
        }

        // Ett enda hash för alla: hashen innehåller sitt eget salt och kontrollerar vilket lösenord som helst som
        // matchar, så 60 olika hashar (med 10 000-tals iterationer vardera) skulle bara göra starten långsammare.
        var passwordHash = passwordHasher.HashPassword(new User(), DemoPassword);
        var existingUsers = await LoadExistingUsersAsync(ct);

        new DemoBuilder(db, school.Id, passwordHash, publicTeacher, publicStudent, existingUsers).Build();
        var created = db.ChangeTracker.Entries().Count(entry => entry.State == EntityState.Added);
        await db.SaveChangesAsync(ct);

        var removed = await RemoveDemoClassAsync(school.Id, ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Demodata skapad i skolan {School}: {Created} rader. Demo-klass och Demo-kurs {Removed}.",
            school.Name,
            created,
            removed
                ? "togs bort"
                : "behölls");
        return true;
    }

    private Task<School?> FindSchoolAsync(CancellationToken ct) => FindSchoolAsync(db, ct);

    internal static async Task<School?> FindSchoolAsync(AppDb db, CancellationToken ct) =>
        await db.Schools.FirstOrDefaultAsync(row => row.Id == PlaceholderIds.SchoolId, ct)
        ?? await db.Schools.OrderBy(row => row.Name).FirstOrDefaultAsync(ct);

    private async Task<User> FindAccountAsync(string username, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(user => user.Email.ToLower() == username, ct)
        ?? throw new InvalidOperationException($"Kontot {username} saknas. AccountSeed ska köras före DemoSeed.");

    /// <summary>
    /// Finns en användare med samma e-post redan återanvänds hen, och lösenordet lämnas orört. Det borde inte
    /// hända, men en seed ska aldrig skriva över någon annans konto.
    /// </summary>
    private async Task<Dictionary<string, User>> LoadExistingUsersAsync(CancellationToken ct)
    {
        var emails = Teachers
            .Concat(Mentors)
            .Concat(Enumerable.Range(0, 80).Select(Student))
            .Select(person => person.Email)
            .ToList();
        return await db.Users.Where(user => emails.Contains(user.Email)).ToDictionaryAsync(user => user.Email, ct);
    }

    /// <summary>
    /// Tar bort Demo-klass och Demo-kurs från AccountSeed, men bara om det är säkert: ingen har lämnat in något,
    /// ingen annan kurs eller medlem hör till klassen och kursen läses inte av någon annan klass.
    /// Annars lämnas båda orörda.
    /// </summary>
    private async Task<bool> RemoveDemoClassAsync(Guid schoolId, CancellationToken ct)
    {
        var demoGroup = await db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(group => group.SchoolId == schoolId && group.Name == AccountSeed.ClassName, ct);
        var demoCourse = await db.Courses.AsNoTracking()
            .FirstOrDefaultAsync(course => course.SchoolId == schoolId && course.Code == AccountSeed.CourseCode, ct);
        if (demoGroup is null && demoCourse is null)
        {
            return false;
        }

        var groupId = demoGroup?.Id ?? Guid.Empty;
        var courseId = demoCourse?.Id ?? Guid.Empty;
        var links = await db.ContentLinks
            .Where(link => link.GroupId == groupId || link.CourseId == courseId)
            .Select(link => new { link.Id, link.ContentId })
            .ToListAsync(ct);
        var linkIds = links.Select(link => link.Id).ToList();
        var contentIds = links.Select(link => link.ContentId).Distinct().ToList();

        var accountIds = await db.Users
            .Where(user => user.Email == "admin" || user.Email == "larare" || user.Email == "elev")
            .Select(user => user.Id)
            .ToListAsync(ct);
        var hasSubmissions = await db.Submissions
            .AnyAsync(submission => linkIds.Contains(submission.ContentLinkId), ct);
        var groupHasOtherCourses = await db.GroupCourses.AnyAsync(
            link => link.GroupId == groupId && link.CourseId != courseId, ct);
        var courseHasOtherGroups = await db.GroupCourses.AnyAsync(
            link => link.CourseId == courseId && link.GroupId != groupId, ct);
        var groupHasOtherMembers = await db.Enrollments.AnyAsync(
            enrollment => enrollment.GroupId == groupId
                && enrollment.UserId != PlaceholderIds.UserId
                && !accountIds.Contains(enrollment.UserId),
            ct);
        if (hasSubmissions || groupHasOtherCourses || courseHasOtherGroups || groupHasOtherMembers)
        {
            return false;
        }

        // Länkarna först (de pekar på modulerna), sedan det som bara de pekade på. Beroenden, klarmarkeringar,
        // kommentarer och bilagor följer med via kaskad.
        await db.ContentLinks.Where(link => linkIds.Contains(link.Id)).ExecuteDeleteAsync(ct);
        await db.Content
            .Where(content => contentIds.Contains(content.Id)
                && !db.ContentLinks.Any(link => link.ContentId == content.Id))
            .ExecuteDeleteAsync(ct);
        await db.Modules.Where(module => module.CourseId == courseId).ExecuteDeleteAsync(ct);
        await db.GroupCourses.Where(link => link.GroupId == groupId).ExecuteDeleteAsync(ct);
        await db.CourseTeachers.Where(courseTeacher => courseTeacher.CourseId == courseId).ExecuteDeleteAsync(ct);
        await db.Enrollments.Where(enrollment => enrollment.GroupId == groupId).ExecuteDeleteAsync(ct);
        await db.Groups.Where(group => group.Id == groupId).ExecuteDeleteAsync(ct);
        await db.Courses.Where(course => course.Id == courseId).ExecuteDeleteAsync(ct);
        return true;
    }
}

/// <summary>Bygger entiteterna för demodatan från katalogen. Sparar inget själv, det gör DemoSeed.</summary>
internal sealed class DemoBuilder(
    AppDb db,
    Guid schoolId,
    string passwordHash,
    User publicTeacher,
    User publicStudent,
    Dictionary<string, User> existingUsers)
{
    // Inlämningar, betyg och kommentarer är daterade senast här, så att inget ligger i framtiden när seeden körs.
    private static readonly DateTimeOffset _cutoff = Se(2026, 10, 29, 9);

    // Eleven i demokontot sitter på plats 6 i .NET25, så att hen inte är först i klasslistan.
    internal const int PublicStudentPosition = 6;

    private readonly Dictionary<string, User> _users = new()
    {
        ["larare"] = publicTeacher,
        ["elev"] = publicStudent
    };

    private readonly List<NewsItem> _news = [];
    private readonly HashSet<(Guid CourseId, Guid UserId)> _taught = [];
    private readonly List<GradedItem> _graded = [];
    private readonly List<AssignmentItem> _assignments = [];
    private int _courseCounter;

    private enum Outcome
    {
        None,
        Submitted,
        NeedsWork,
        Graded
    }

    public void Build()
    {
        foreach (var classSpec in DemoCatalog.Classes())
        {
            BuildClass(classSpec);
        }

        AddNotificationsForPublicAccounts();
    }

    private void BuildClass(ClassSpec spec)
    {
        var group = new Group { SchoolId = schoolId, Type = spec.Type, Name = spec.Name, Term = spec.Term };
        db.Groups.Add(group);

        foreach (var key in spec.Teachers)
        {
            db.Enrollments
                .Add(new Enrollment { UserId = UserOf(key).Id, GroupId = group.Id, Role = GroupRole.Teacher });
        }

        if (spec.Mentor is not null)
        {
            db.Enrollments.Add(
                new Enrollment { UserId = UserOf(spec.Mentor).Id, GroupId = group.Id, Role = GroupRole.Mentor });
        }

        var students = Enumerable.Range(spec.FirstStudent, spec.StudentCount)
            .Select(index => UserOf(Student(index).Key))
            .ToList();
        if (spec.HasPublicStudent)
        {
            students.Insert(Math.Min(PublicStudentPosition, students.Count), publicStudent);
        }

        foreach (var student in students)
        {
            db.Enrollments.Add(new Enrollment { UserId = student.Id, GroupId = group.Id, Role = GroupRole.Student });
        }

        foreach (var courseSpec in spec.Courses)
        {
            BuildCourse(group, courseSpec, students);
        }

        foreach (var news in spec.News)
        {
            AddNews(group, null, news, students);
        }
    }

    private void BuildCourse(Group group, CourseSpec spec, List<User> students)
    {
        _courseCounter++;
        var course = new Course
        {
            SchoolId = schoolId,
            Code = spec.Code,
            Title = spec.Title,
            Description = spec.Description,
            Points = spec.Points,
            StartsOn = spec.Start,
            EndsOn = spec.End
        };
        db.Courses.Add(course);
        db.GroupCourses.Add(new GroupCourse { GroupId = group.Id, CourseId = course.Id });
        foreach (var key in spec.Teachers)
        {
            var teacher = UserOf(key);
            db.CourseTeachers.Add(new CourseTeacher { CourseId = course.Id, UserId = teacher.Id });
            _taught.Add((course.Id, teacher.Id));
        }

        var author = UserOf(spec.Author);
        for (var moduleIndex = 0; moduleIndex < spec.Modules.Length; moduleIndex++)
        {
            BuildModule(group, course, spec, spec.Modules[moduleIndex], moduleIndex, author, students);
        }

        foreach (var news in spec.News)
        {
            AddNews(group, course, news, students);
        }
    }

    private void BuildModule(
        Group group,
        Course course,
        CourseSpec courseSpec,
        ModuleSpec spec,
        int moduleIndex,
        User author,
        List<User> students)
    {
        var module = NewModule(course.Id, spec, moduleIndex + 1);
        db.Modules.Add(module);

        var publishAt = PartPublishAt(courseSpec, spec);
        var built = new List<(PartSpec Spec, Content Content, ContentLink Link)>();
        foreach (var part in spec.Parts)
        {
            var (content, link) = NewPart(part, author.Id, group.Id, course.Id, module.Id, built.Count + 1, publishAt);
            db.Content.Add(content);
            db.ContentLinks.Add(link);
            built.Add((part, content, link));
        }

        if (spec.Sequential)
        {
            var theory = built.Where(item => item.Spec.Type == ContentType.Theory).ToList();
            for (var index = 1; index < theory.Count; index++)
            {
                db.ContentLinkDependencies.Add(new ContentLinkDependency
                {
                    BlockedLinkId = theory[index].Link.Id,
                    BlockingLinkId = theory[index - 1].Link.Id
                });
            }
        }

        var salt = (_courseCounter * 100) + moduleIndex;
        AddCompletions(spec, built.Select(item => (item.Spec, item.Link)).ToList(), students, salt);
        AddSubmissions(group, built, author, students, salt);
    }

    /// <summary>
    /// Klarmarkeringar av teori och läsuppgifter. Eleven klarar antingen allt i veckan, allt utom sista delen eller
    /// inget, så att en låst del aldrig är klarmarkerad före delen som låser den. Veckor som inte öppnat än får inga.
    /// </summary>
    private void AddCompletions(
        ModuleSpec module, List<(PartSpec Spec, ContentLink Link)> parts, List<User> students, int salt)
    {
        if (module.PublishAt > _cutoff)
        {
            return;
        }

        var manual = parts
            .Where(part => part.Spec.Type == ContentType.Theory || part.Spec.Modes.Length == 0)
            .ToList();
        for (var index = 0; index < students.Count; index++)
        {
            var roll = Roll(salt, index, 7);
            int count;
            if (students[index].Id == publicStudent.Id)
            {
                count = module.Completion >= 85
                    ? manual.Count
                    : module.Completion >= 50
                        ? manual.Count - 1
                        : 0;
            }
            else if (roll < module.Completion)
            {
                count = manual.Count;
            }
            else if (roll < module.Completion + 8)
            {
                count = manual.Count - 1;
            }
            else
            {
                count = 0;
            }

            for (var part = 0; part < count; part++)
            {
                db.PartCompletions.Add(new PartCompletion
                {
                    ContentLinkId = manual[part].Link.Id,
                    StudentId = students[index].Id,
                    CompletedAt = Earlier(manual[part].Link.PublishAt.AddHours(8 + Roll(salt, index, part)), 1)
                });
            }
        }
    }

    private void AddSubmissions(
        Group group,
        List<(PartSpec Spec, Content Content, ContentLink Link)> parts,
        User author,
        List<User> students,
        int salt)
    {
        foreach (var (spec, content, link) in parts)
        {
            if (spec.Type != ContentType.Assignment || spec.Modes.Length == 0)
            {
                continue;
            }

            _assignments.Add(new AssignmentItem(group.Id, content, link));
            if (spec.Pattern == SubmissionPattern.None)
            {
                continue;
            }

            var partSalt = (salt * 100) + link.Position;
            for (var index = 0; index < students.Count; index++)
            {
                var student = students[index];
                var isPublicStudent = student.Id == publicStudent.Id;
                var outcome = Decide(spec.Pattern, Roll(partSalt, index, 3), isPublicStudent);
                if (outcome != Outcome.None)
                {
                    AddSubmission(spec, link, author, student, outcome, partSalt, index);
                }
            }
        }
    }

    private static Outcome Decide(SubmissionPattern pattern, int roll, bool isPublicStudent) => pattern switch
    {
        SubmissionPattern.Finished => Outcome.Graded,
        SubmissionPattern.MostlyGraded => isPublicStudent
            ? Outcome.Graded
            : roll < 5
                ? Outcome.None
                : roll < 12
                    ? Outcome.NeedsWork
                    : Outcome.Graded,
        SubmissionPattern.Queue => isPublicStudent
            ? Outcome.NeedsWork
            : roll < 12
                ? Outcome.None
                : roll < 22
                    ? Outcome.NeedsWork
                    : roll < 50
                        ? Outcome.Graded
                        : Outcome.Submitted,
        SubmissionPattern.Early => isPublicStudent
            ? Outcome.Submitted
            : roll < 35
                ? Outcome.Submitted
                : Outcome.None,
        _ => Outcome.None
    };

    private void AddSubmission(
        PartSpec spec, ContentLink link, User teacher, User student, Outcome outcome, int partSalt, int index)
    {
        var due = spec.DueAt!.Value;
        var roll = Roll(partSalt, index, 11);

        // Lämnar in en till tre dygn före deadline, men aldrig senare än att det hunnit hända.
        var submittedAt = due.AddHours(-(2 + (roll % 60)));
        if (submittedAt > _cutoff)
        {
            submittedAt = _cutoff.AddHours(-(1 + (roll % 40)));
        }

        var submission = new Submission
        {
            ContentLinkId = link.Id,
            StudentId = student.Id,
            Status = outcome switch
            {
                Outcome.Graded => SubmissionStatus.Graded,
                Outcome.NeedsWork => SubmissionStatus.NeedsWork,
                _ => SubmissionStatus.Submitted
            },
            SubmittedAt = submittedAt,
            BodyMd = spec.Modes.Contains(SubmissionMode.Text)
                ? spec.Answers[roll % spec.Answers.Length]
                : null
        };

        if (outcome is Outcome.Graded or Outcome.NeedsWork)
        {
            var gradedAt = Earlier((due > submittedAt
                ? due
                : submittedAt).AddHours(6 + (roll % 48)), 0);
            var level = outcome == Outcome.Graded
                ? LevelOf(spec.Scale, roll)
                : (GradeLevel?)null;
            submission.Grade = new Grade
            {
                SubmissionId = submission.Id,
                GradedById = teacher.Id,
                Level = level,
                Feedback = FeedbackFor(spec, level, roll),
                GradedAt = gradedAt
            };
            _graded.Add(new GradedItem(student.Id, link, submission, gradedAt));
        }

        db.Submissions.Add(submission);
    }

    private static GradeLevel LevelOf(GradeScale scale, int roll) =>
        scale == GradeScale.PassFailDistinction && roll % 100 < 30
            ? GradeLevel.Distinction
            : GradeLevel.Pass;

    private static string FeedbackFor(PartSpec spec, GradeLevel? level, int roll)
    {
        var pool = level switch
        {
            null => spec.NeedsWork.Length > 0
                ? spec.NeedsWork
                : DemoCatalog.NeedsWorkFeedback,
            GradeLevel.Distinction => DemoCatalog.DistinctionFeedback,
            _ => spec.Feedback.Length > 0
                ? spec.Feedback
                : DemoCatalog.PassFeedback
        };
        return pool[(roll / 3) % pool.Length];
    }

    private void AddNews(Group group, Course? course, NewsSpec spec, List<User> students)
    {
        var author = UserOf(spec.Author);
        var (content, link) = NewNews(spec, author.Id, group.Id, course?.Id);
        db.Content.Add(content);
        db.ContentLinks.Add(link);
        _news.Add(new NewsItem(group.Id, link.CourseId, content, spec.At, author.Id));

        foreach (var comment in spec.Comments)
        {
            db.Comments.Add(NewComment(content.Id, CommentAuthor(comment.Author, students).Id, spec, comment));
        }
    }

    // --- Delas med DemoContentRefresh, så att nytt innehåll i prod blir exakt som det seeden hade skapat ---

    internal static Module NewModule(Guid courseId, ModuleSpec spec, int position) => new()
    {
        CourseId = courseId,
        Title = spec.Title,
        Description = spec.Description,
        Position = position,
        PublishAt = spec.PublishAt
    };

    /// <summary>
    /// När veckans delar publiceras. En vecka utan eget publiceringsdatum är öppen direkt, även i en kurs som
    /// startar längre fram.
    /// </summary>
    internal static DateTimeOffset PartPublishAt(CourseSpec courseSpec, ModuleSpec spec)
    {
        var courseStart = Se(courseSpec.Start, 7);
        return spec.PublishAt ?? (courseStart < _cutoff.AddDays(-7)
            ? courseStart
            : _cutoff.AddDays(-7));
    }

    internal static (Content Content, ContentLink Link) NewPart(
        PartSpec part,
        Guid authorId,
        Guid groupId,
        Guid courseId,
        Guid moduleId,
        int position,
        DateTimeOffset publishAt)
    {
        var content = new Content
        {
            AuthorId = authorId,
            Type = part.Type,
            Title = part.Title,
            BodyMd = part.Body,
            Status = ContentStatus.Published,
            CreatedAt = publishAt.AddDays(-3),
            Assignment = part.Type == ContentType.Assignment
                ? new AssignmentSpec { GradeScale = part.Scale, SubmissionModes = [.. part.Modes] }
                : null
        };
        var link = new ContentLink
        {
            ContentId = content.Id,
            GroupId = groupId,
            CourseId = courseId,
            ModuleId = moduleId,
            Position = position,
            PublishAt = publishAt,
            DueAt = part.DueAt
        };
        return (content, link);
    }

    internal static (Content Content, ContentLink Link) NewNews(
        NewsSpec spec, Guid authorId, Guid groupId, Guid? courseId)
    {
        var content = new Content
        {
            AuthorId = authorId,
            Type = ContentType.News,
            Title = spec.Title,
            BodyMd = spec.Body,
            Status = ContentStatus.Published,
            CreatedAt = spec.At
        };
        var link = new ContentLink
        {
            ContentId = content.Id,
            GroupId = groupId,
            CourseId = spec.InCourse
                ? courseId
                : null,
            IsPinned = spec.Pinned,
            PublishAt = spec.At
        };
        return (content, link);
    }

    internal static Comment NewComment(Guid contentId, Guid authorId, NewsSpec news, CommentSpec comment) => new()
    {
        ContentId = contentId,
        AuthorId = authorId,
        BodyMd = comment.Body,
        CreatedAt = news.At.AddHours(comment.HoursAfter)
    };

    /// <summary>
    /// Inkorgen för de två offentliga kontona, räknad på det som hänt de senaste veckorna. Andra användare får
    /// sina notiser av notisjobbet för det som publiceras inom några dagar, och jobbet hoppar över dessa.
    /// </summary>
    private void AddNotificationsForPublicAccounts()
    {
        var since = _cutoff.AddDays(-14);
        var memberGroups = db.Enrollments.Local
            .Where(enrollment => enrollment.UserId == publicStudent.Id || enrollment.UserId == publicTeacher.Id)
            .ToList();

        foreach (var user in new[] { publicStudent, publicTeacher })
        {
            var groupIds = memberGroups.Where(enrollment => enrollment.UserId == user.Id)
                .Select(enrollment => enrollment.GroupId)
                .ToHashSet();

            // Nyheter i en kurs når läraren bara om hen är kursens lärare, klassens nyheter når klassens lärare.
            foreach (var news in _news.Where(item => groupIds.Contains(item.GroupId)
                         && item.AuthorId != user.Id
                         && item.At >= since
                         && (user.Id == publicStudent.Id
                             || item.CourseId is null
                             || _taught.Contains((item.CourseId.Value, user.Id)))))
            {
                AddNotification(
                    user,
                    NotificationKind.NewsPublished,
                    news.Content.Id,
                    null,
                    news.At,
                    _cutoff.AddDays(-5));
            }
        }

        foreach (var assignment in _assignments.Where(item =>
                     item.Link.PublishAt >= since && item.Link.PublishAt <= _cutoff
                     && memberGroups.Any(enrollment => enrollment.UserId == publicStudent.Id
                         && enrollment.GroupId == item.GroupId)))
        {
            AddNotification(
                publicStudent,
                NotificationKind.AssignmentPublished,
                assignment.Content.Id,
                null,
                assignment.Link.PublishAt,
                _cutoff.AddDays(-5));
        }

        foreach (var graded in _graded.Where(item => item.StudentId == publicStudent.Id && item.GradedAt >= since))
        {
            var kind = graded.Submission.Status == SubmissionStatus.Graded
                ? NotificationKind.SubmissionGraded
                : NotificationKind.SubmissionNeedsWork;
            AddNotification(
                publicStudent,
                kind,
                graded.Link.ContentId,
                graded.Submission.Id,
                graded.GradedAt,
                _cutoff.AddDays(-2));
        }
    }

    // Äldre än unreadFrom räknas som lästa.
    private void AddNotification(
        User user,
        NotificationKind kind,
        Guid contentId,
        Guid? submissionId,
        DateTimeOffset createdAt,
        DateTimeOffset unreadFrom) =>
        db.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Kind = kind,
            ContentId = contentId,
            SubmissionId = submissionId,
            CreatedAt = createdAt,
            ReadAt = createdAt < unreadFrom
                ? createdAt.AddHours(3)
                : null
        });

    // Ingenting som seeden skapar ska vara daterat efter _cutoff, men inte heller före tidpunkten själv.
    private static DateTimeOffset Earlier(DateTimeOffset time, int marginHours) =>
        time > _cutoff.AddHours(-marginHours)
            ? _cutoff.AddHours(-marginHours)
            : time;

    private User CommentAuthor(string reference, List<User> students) =>
        reference.StartsWith('#')
            ? students[int.Parse(reference[1..])]
            : UserOf(reference);

    /// <summary>Användaren för en nyckel (lärare, mentor, elevN, larare eller elev). Skapas första gången den behövs.</summary>
    private User UserOf(string key)
    {
        if (_users.TryGetValue(key, out var known))
        {
            return known;
        }

        var person = Teachers.Concat(Mentors).FirstOrDefault(candidate => candidate.Key == key)
            ?? Student(int.Parse(key["elev".Length..]));
        var role = Teachers.Any(teacher => teacher.Key == key) || Mentors.Any(mentor => mentor.Key == key)
            ? UserRole.Teacher
            : UserRole.Student;

        if (!existingUsers.TryGetValue(person.Email, out var user))
        {
            user = new User
            {
                SchoolId = schoolId,
                Name = person.Name,
                Email = person.Email,
                Role = role,
                PasswordHash = passwordHash
            };
            db.Users.Add(user);
        }

        _users[key] = user;
        return user;
    }

    private sealed record NewsItem(Guid GroupId, Guid? CourseId, Content Content, DateTimeOffset At, Guid AuthorId);

    private sealed record GradedItem(Guid StudentId, ContentLink Link, Submission Submission, DateTimeOffset GradedAt);

    private sealed record AssignmentItem(Guid GroupId, Content Content, ContentLink Link);
}
