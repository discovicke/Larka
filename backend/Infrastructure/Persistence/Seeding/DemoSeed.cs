using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Seeding;

/// <summary>
/// Kompakt demodata för en kort demonstration: tre klasser med en avslutad, en pågående och en kommande kurs,
/// åtta elever per klass och inlämningar i blandade lägen. Kontona admin, larare och elev skapas av AccountSeed.
/// </summary>
public sealed class DemoSeed(
    AppDb db,
    IPasswordHasher<User> passwordHasher,
    TimeProvider time,
    ILogger<DemoSeed> logger)
{
    public const string DemoPassword = "larka-demo";
    public const string MarkerGroupName = ".NET25";

    private static readonly string[] _studentNames =
    [
        "Anna Andersson", "Erik Johansson", "Sara Karlsson", "Oscar Nilsson",
        "Elin Eriksson", "Hugo Larsson", "Maja Olsson", "Liam Persson",
        "Alva Svensson", "Noah Gustafsson", "Ella Pettersson", "William Jonsson",
        "Iris Jansson", "Adam Hansson", "Freja Bengtsson", "Lucas Lindberg",
        "Nova Jakobsson", "Isak Magnusson", "Signe Lindqvist", "Leo Berg",
        "Sofia Ström", "Viktor Ek", "Tilda Holm", "Axel Dahl",
    ];

    // Kurskoden följer Edugrades mönster: klassens kod, understreck och en kort förkortning av kursen
    // (.NET25_DevOps). Samma mönster ger förslaget när admin skapar en ny kurs från en tidigare.
    private static readonly Dictionary<string, ClassPlan> _plans = new()
    {
        [".NET25"] = new ClassPlan(
            new DemoCourse("WeApp", "Webbapplikationer i C#, ASP.NET",
                "Eleverna bygger en webbapplikation med ASP.NET Core och Entity Framework."),
            new DemoCourse("DevOps", "DevOps",
                "Versionshantering, tester, CI/CD och driftsättning i ett större grupprojekt."),
            new DemoCourse("LIA", "LIA",
                "Lärande i arbete på ett företag, med rapporter till skolan under perioden."),
            70),
        ["JAVA25"] = new ClassPlan(
            new DemoCourse("OPJ", "Objektorienterad programmering med Java",
                "Grunderna i Java: typer, metoder, klasser och första konsolprogrammet."),
            new DemoCourse("SpBo", "Webbtjänster med Spring Boot",
                "Eleverna bygger en REST-tjänst med Spring Boot och JPA i en större gemensam uppgift."),
            new DemoCourse("MjTe", "Mjukvarutestning",
                "Enhetstester, integrationstester och testautomatisering för Spring-tjänsten."),
            70),
        ["FIBER25"] = new ClassPlan(
            new DemoCourse("FiTe", "Fiberteknik",
                "Fiberns uppbyggnad, skarvning och mätning med OTDR."),
            new DemoCourse("NaUp", "Nätuppbyggnad",
                "Eleverna planerar och bygger ett mindre fibernät från nod till fastighet."),
            new DemoCourse("FaPr", "Fältprojekt",
                "Ett projekt ute på fält där eleverna driftsätter och dokumenterar ett nät."),
            84),
    };

    // Äldre demodata använde koder som NET-NUT (bas, nuvarande, fördjupning) och titlar som "C# 1". De byts mot
    // de nya en gång, när demodatan redan finns. Kurser som admin har skapat själv rörs inte.
    private static readonly Dictionary<string, (string Prefix, string[] Titles)> _legacy = new()
    {
        [".NET25"] = ("NET", ["C# 1", "C# i praktiken", "Fördjupning"]),
        ["JAVA25"] = ("JAVA", ["Java 1", "Java i praktiken", "Fördjupning"]),
        ["FIBER25"] = ("FIBER", ["Fiber 1", "Nätuppbyggnad", "Fältprojekt"]),
    };

    private static readonly string[] _legacySuffixes = ["BAS", "NUT", "FORD"];

    private static readonly string[] _legacyDescriptions =
    [
        "Grundkurs med fokus på begrepp, verktyg och första leveransen.",
        "Pågående kurs där eleverna arbetar med en större gemensam uppgift.",
        "Kommande kurs som bygger vidare på den pågående kursen.",
    ];

    internal static string CodeFor(string className, DemoCourse course) =>
        $"{className}_{course.CodeSuffix}";

    public async Task<bool> SeedAsync(CancellationToken ct)
    {
        var school = await FindSchoolAsync(db, ct);
        if (school is null)
        {
            logger.LogWarning("Demodata hoppades över: det finns ingen skola.");
            return false;
        }

        if (await db.Groups.AnyAsync(group => group.SchoolId == school.Id && group.Name == MarkerGroupName, ct))
        {
            logger.LogInformation("Demodata finns redan i skolan {School}, inget lades till.", school.Name);
            await RenameLegacyCoursesAsync(school.Id, ct);
            return false;
        }

        var teacher = await db.Users.SingleAsync(user => user.Email == "larare", ct);
        var publicStudent = await db.Users.SingleAsync(user => user.Email == "elev", ct);
        var studentHash = passwordHasher.HashPassword(new User(), DemoPassword);
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var net = BuildClass(".NET25", today, teacher, studentHash, publicStudent, 0, 18);
        var java = BuildClass("JAVA25", today, teacher, studentHash, null, 8, 35);
        var fiber = BuildClass("FIBER25", today, teacher, studentHash, null, 16, 50);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Kompakt demodata skapad: {Net}, {Java}, {Fiber}.",
            net.Name,
            java.Name,
            fiber.Name);
        return true;
    }

    private async Task RenameLegacyCoursesAsync(Guid schoolId, CancellationToken ct)
    {
        var renamed = 0;
        foreach (var (className, (prefix, oldTitles)) in _legacy)
        {
            var plan = _plans[className];
            DemoCourse[] courses = [plan.Completed, plan.Current, plan.Upcoming];
            for (var index = 0; index < courses.Length; index += 1)
            {
                var oldCode = $"{prefix}-{_legacySuffixes[index]}";
                var course = await db.Courses.SingleOrDefaultAsync(
                    row => row.SchoolId == schoolId && row.Code == oldCode, ct);
                var newCode = CodeFor(className, courses[index]);
                if (course is null || await db.Courses.AnyAsync(
                        row => row.SchoolId == schoolId && row.Code == newCode, ct))
                {
                    continue;
                }

                course.Code = newCode;
                renamed += 1;
                if (course.Title != oldTitles[index])
                {
                    continue;
                }

                var oldTitle = course.Title;
                var newTitle = courses[index].Title;
                course.Title = newTitle;
                if (_legacyDescriptions.Contains(course.Description))
                {
                    course.Description = courses[index].Description;
                }

                var contentIds = await db.ContentLinks
                    .Where(link => link.CourseId == course.Id)
                    .Select(link => link.ContentId)
                    .ToListAsync(ct);
                var content = await db.Content.Where(row => contentIds.Contains(row.Id)).ToListAsync(ct);
                foreach (var row in content)
                {
                    row.Title = row.Title == $"Introduktion till {oldTitle}"
                        ? $"Introduktion till {newTitle}"
                        : row.Title.EndsWith($" – {oldTitle}", StringComparison.Ordinal)
                            ? row.Title[..^oldTitle.Length] + newTitle
                            : row.Title;
                    row.BodyMd = row.BodyMd.Replace($"**{oldTitle}**", $"**{newTitle}**", StringComparison.Ordinal);
                }

                var groupIds = await db.GroupCourses
                    .Where(link => link.CourseId == course.Id)
                    .Select(link => link.GroupId)
                    .ToListAsync(ct);
                var newsIds = await db.ContentLinks
                    .Where(link => link.GroupId != null && groupIds.Contains(link.GroupId.Value))
                    .Select(link => link.ContentId)
                    .ToListAsync(ct);
                var news = await db.Content
                    .Where(row => newsIds.Contains(row.Id) && row.Type == ContentType.News)
                    .ToListAsync(ct);
                foreach (var row in news)
                {
                    row.BodyMd = row.BodyMd.Replace($"**{oldTitle}**", $"**{newTitle}**", StringComparison.Ordinal);
                }
            }
        }

        if (renamed > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Demodatans kurskoder byttes till KLASS_KURS för {Count} kurser.", renamed);
        }
    }

    internal static async Task<School?> FindSchoolAsync(AppDb db, CancellationToken ct) =>
        await db.Schools.FirstOrDefaultAsync(row => row.Id == PlaceholderIds.SchoolId, ct)
        ?? await db.Schools.OrderBy(row => row.Name).FirstOrDefaultAsync(ct);

    private Group BuildClass(
        string name,
        DateOnly today,
        User teacher,
        string studentHash,
        User? publicStudent,
        int studentOffset,
        int currentCourseDaysLeft)
    {
        var group = new Group { SchoolId = teacher.SchoolId, Type = GroupType.Class, Name = name };
        db.Groups.Add(group);
        db.Enrollments.Add(new Enrollment { GroupId = group.Id, UserId = teacher.Id, Role = GroupRole.Teacher });

        var students = new List<User>();
        if (publicStudent is not null)
        {
            students.Add(publicStudent);
            db.Enrollments.Add(new Enrollment
            {
                GroupId = group.Id,
                UserId = publicStudent.Id,
                Role = GroupRole.Student
            });
        }

        for (var index = students.Count; index < 8; index += 1)
        {
            var student = new User
            {
                SchoolId = teacher.SchoolId,
                Name = _studentNames[studentOffset + index],
                Email = $"{name.ToLowerInvariant()}.{index + 1}@larka.local",
                Role = UserRole.Student,
                PasswordHash = studentHash
            };
            db.Users.Add(student);
            students.Add(student);
            db.Enrollments.Add(new Enrollment
            {
                GroupId = group.Id,
                UserId = student.Id,
                Role = GroupRole.Student
            });
        }

        var plan = _plans[name];
        var completed = BuildCourse(
            group,
            teacher,
            students,
            today.AddDays(-210),
            today.AddDays(-140),
            plan.Completed,
            CoursePhase.Completed);
        var current = BuildCourse(
            group,
            teacher,
            students,
            today.AddDays(-60),
            today.AddDays(currentCourseDaysLeft),
            plan.Current,
            CoursePhase.Current);
        var upcoming = BuildCourse(
            group,
            teacher,
            students,
            today.AddDays(45),
            today.AddDays(45 + plan.UpcomingDays),
            plan.Upcoming,
            CoursePhase.Upcoming);

        AddCourseNews(group, teacher, completed, current, upcoming);
        return group;
    }

    private Course BuildCourse(
        Group group,
        User teacher,
        IReadOnlyList<User> students,
        DateOnly startsOn,
        DateOnly endsOn,
        DemoCourse demo,
        CoursePhase phase)
    {
        var title = demo.Title;
        var course = new Course
        {
            SchoolId = teacher.SchoolId,
            Title = title,
            Code = CodeFor(group.Name, demo),
            Description = demo.Description,
            Points = phase == CoursePhase.Completed
                ? 20
                : 25,
            StartsOn = startsOn,
            EndsOn = endsOn
        };
        db.Courses.Add(course);
        db.GroupCourses.Add(new GroupCourse { GroupId = group.Id, CourseId = course.Id });
        db.CourseTeachers.Add(new CourseTeacher { CourseId = course.Id, UserId = teacher.Id });

        var start = AtStartOfDay(startsOn);
        var first = new Module
        {
            CourseId = course.Id,
            Title = "Vecka 1",
            Description = "Introduktion och första uppgiften.",
            Position = 1,
            PublishAt = start
        };
        var second = new Module
        {
            CourseId = course.Id,
            Title = "Vecka 2",
            Description = "Fördjupning och nästa leverans.",
            Position = 2,
            PublishAt = start.AddDays(21)
        };
        db.Modules.AddRange(first, second);

        var theory = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.Theory,
            Title = $"Introduktion till {title}",
            BodyMd = $"Kort introduktion till **{title}**. Materialet är avsiktligt kort i demon.",
            Status = ContentStatus.Published
        };
        db.Content.Add(theory);
        db.ContentLinks.Add(new ContentLink
        {
            ContentId = theory.Id,
            CourseId = course.Id,
            ModuleId = first.Id,
            Position = 1,
            PublishAt = start
        });

        var assignments = phase switch
        {
            CoursePhase.Completed => new[]
            {
                CreateAssignment(course, teacher, first, title, 1, start.AddDays(14)),
                CreateAssignment(course, teacher, first, title, 2, start.AddDays(35)),
                CreateAssignment(course, teacher, second, title, 3, start.AddDays(56)),
            },
            CoursePhase.Current => new[]
            {
                CreateAssignment(course, teacher, first, title, 1, start.AddDays(14)),
                CreateAssignment(course, teacher, first, title, 2, start.AddDays(35)),
                CreateAssignment(course, teacher, second, title, 3, start.AddDays(56)),
                CreateAssignment(course, teacher, second, title, 4, start.AddDays(77)),
            },
            _ => new[]
            {
                CreateAssignment(course, teacher, first, title, 1, start.AddDays(14)),
                CreateAssignment(course, teacher, second, title, 2, start.AddDays(42)),
            },
        };

        if (phase != CoursePhase.Upcoming)
        {
            AddSubmissions(course, teacher, students, assignments, phase);
        }

        return course;
    }

    private ContentLink CreateAssignment(
        Course course, User teacher, Module module, string courseTitle, int number, DateTimeOffset dueAt)
    {
        var content = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.Assignment,
            Title = $"Inlämning {number} – {courseTitle}",
            BodyMd = $"Beskriv hur du har löst uppgift {number}. Feedbacken blir kort i demon.",
            Status = ContentStatus.Published,
            Assignment = new AssignmentSpec
            {
                GradeScale = GradeScale.PassFailDistinction,
                SubmissionModes = [SubmissionMode.Text],
                CompletionRule = CompletionRule.Submitted
            }
        };
        var link = new ContentLink
        {
            ContentId = content.Id,
            CourseId = course.Id,
            ModuleId = module.Id,
            Position = number + 1,
            PublishAt = dueAt.AddDays(-10),
            DueAt = dueAt
        };
        db.Content.Add(content);
        db.ContentLinks.Add(link);
        return link;
    }

    private void AddSubmissions(
        Course course,
        User teacher,
        IReadOnlyList<User> students,
        IReadOnlyList<ContentLink> assignments,
        CoursePhase phase)
    {
        for (var studentIndex = 0; studentIndex < students.Count; studentIndex += 1)
        {
            for (var assignmentIndex = 0; assignmentIndex < assignments.Count; assignmentIndex += 1)
            {
                var status = StatusFor(phase, assignmentIndex, studentIndex);
                if (status is null)
                {
                    continue;
                }

                var submission = new Submission
                {
                    ContentLinkId = assignments[assignmentIndex].Id,
                    StudentId = students[studentIndex].Id,
                    Status = status.Value,
                    SubmittedAt = status == SubmissionStatus.NotStarted
                        ? null
                        : assignments[assignmentIndex].DueAt!.Value.AddDays(-2),
                    BodyMd = status == SubmissionStatus.NotStarted
                        ? null
                        : "Min lösning finns i texten.",
                    PersonalDueAt = PersonalDueAtFor(
                        phase, assignmentIndex, studentIndex, status.Value, assignments[assignmentIndex])
                };
                db.Submissions.Add(submission);

                if (status == SubmissionStatus.Graded)
                {
                    var level = GradeFor(studentIndex, assignmentIndex);
                    db.Grades.Add(new Grade
                    {
                        SubmissionId = submission.Id,
                        GradedById = teacher.Id,
                        Level = level,
                        Feedback = level == GradeLevel.Fail
                            ? "Du behöver komplettera innan uppgiften är godkänd."
                            : level == GradeLevel.Pass
                                ? "Godkänt. Förtydliga gärna dina val nästa gång."
                                : "Mycket väl genomfört med tydlig struktur.",
                        GradedAt = assignments[assignmentIndex].DueAt!.Value.AddDays(2)
                    });
                }
                else if (status == SubmissionStatus.NeedsWork)
                {
                    db.Grades.Add(new Grade
                    {
                        SubmissionId = submission.Id,
                        GradedById = teacher.Id,
                        Level = null,
                        Feedback = "Komplettera det som saknas och lämna in igen.",
                        GradedAt = assignments[assignmentIndex].DueAt!.Value.AddDays(2)
                    });
                }
            }
        }
    }

    // En elev som inte lämnat in på en uppgift där klassens deadline har gått får en extra deadline, så att
    // funktionen syns i demon. Den ligger i framtiden, så eleven slipper automatiskt IG.
    private DateTimeOffset? PersonalDueAtFor(
        CoursePhase phase, int assignmentIndex, int studentIndex, SubmissionStatus status, ContentLink link) =>
        phase == CoursePhase.Current
        && assignmentIndex == 2
        && studentIndex % 10 == 2
        && status == SubmissionStatus.NotStarted
        && link.DueAt < time.GetUtcNow()
            ? time.GetUtcNow().AddDays(5)
            : null;

    private static SubmissionStatus? StatusFor(CoursePhase phase, int assignmentIndex, int studentIndex)
    {
        if (phase == CoursePhase.Completed || assignmentIndex < 2)
        {
            return SubmissionStatus.Graded;
        }

        if (assignmentIndex == 2)
        {
            return (studentIndex % 10) switch
            {
                0 or 1 => SubmissionStatus.NeedsWork,
                2 or 4 => SubmissionStatus.NotStarted,
                3 => SubmissionStatus.Submitted,
                _ => SubmissionStatus.Graded,
            };
        }

        return studentIndex % 3 == 0
            ? SubmissionStatus.Submitted
            : SubmissionStatus.NotStarted;
    }

    private static GradeLevel GradeFor(int studentIndex, int assignmentIndex)
    {
        var roll = (studentIndex * 7 + assignmentIndex * 3) % 10;
        return roll < 2
            ? GradeLevel.Fail
            : roll < 7
                ? GradeLevel.Pass
                : GradeLevel.Distinction;
    }

    private void AddCourseNews(Group group, User teacher, params Course[] courses)
    {
        var current = courses[1];
        var content = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.News,
            Title = $"Välkommen till {group.Name}",
            BodyMd = $"Vi har kickat igång **{current.Title}**. Schemat och uppgifterna ligger i kursen.",
            Status = ContentStatus.Published,
            CreatedAt = time.GetUtcNow().AddDays(-2)
        };
        db.Content.Add(content);
        db.ContentLinks.Add(new ContentLink
        {
            ContentId = content.Id,
            GroupId = group.Id,
            PublishAt = time.GetUtcNow().AddDays(-2),
            IsPinned = true
        });
    }

    private static DateTimeOffset AtStartOfDay(DateOnly value) =>
        new(value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    internal sealed record DemoCourse(string CodeSuffix, string Title, string Description);

    private sealed record ClassPlan(DemoCourse Completed, DemoCourse Current, DemoCourse Upcoming, int UpcomingDays);

    private enum CoursePhase
    {
        Completed,
        Current,
        Upcoming
    }
}
