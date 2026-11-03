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

        var codePrefix = name.TrimStart('.').Replace("25", "");
        var program = name switch
        {
            ".NET25" => "C#",
            "JAVA25" => "Java",
            _ => "Fiber"
        };
        var completed = BuildCourse(
            group,
            teacher,
            students,
            today.AddDays(-210),
            today.AddDays(-140),
            $"{program} 1",
            $"{codePrefix}-BAS",
            "Grundkurs med fokus på begrepp, verktyg och första leveransen.",
            CoursePhase.Completed);
        var current = BuildCourse(
            group,
            teacher,
            students,
            today.AddDays(-60),
            today.AddDays(currentCourseDaysLeft),
            program == "Fiber"
                ? "Nätuppbyggnad"
                : $"{program} i praktiken",
            $"{codePrefix}-NUT",
            "Pågående kurs där eleverna arbetar med en större gemensam uppgift.",
            CoursePhase.Current);
        var upcoming = BuildCourse(
            group,
            teacher,
            students,
            today.AddDays(45),
            today.AddDays(45 + (program == "Fiber"
                ? 84
                : 70)),
            program == "Fiber"
                ? "Fältprojekt"
                : "Fördjupning",
            $"{codePrefix}-FORD",
            "Kommande kurs som bygger vidare på den pågående kursen.",
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
        string title,
        string code,
        string description,
        CoursePhase phase)
    {
        var course = new Course
        {
            SchoolId = teacher.SchoolId,
            Title = title,
            Code = code,
            Description = description,
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
                    SubmittedAt = status == SubmissionStatus.NotSubmitted
                        ? null
                        : assignments[assignmentIndex].DueAt!.Value.AddDays(-2),
                    BodyMd = status == SubmissionStatus.NotSubmitted
                        ? null
                        : "Min lösning finns i texten."
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
                2 => SubmissionStatus.NotSubmitted,
                3 => SubmissionStatus.Submitted,
                4 => SubmissionStatus.NotStarted,
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

    private enum CoursePhase
    {
        Completed,
        Current,
        Upcoming
    }
}
