using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence;

/// <summary>
/// Exempeldata för lokal utveckling: en klass med två kurser (och NET26 att skapa en kursomgång för), två veckor i DevOps, en lärare, två elever
/// och lite innehåll. Körs av EF Core efter Migrate när Database:SeedDevData är true, och bara om det inte
/// finns några grupper än. Placeholder-skolan och placeholder-användaren finns redan via migrationen.
/// Användar-id:na är fasta så att de går att känna igen i dev-databasen och i testerna.
/// Alla seedade användare får lösenordet DevPassword, som bara finns här (dev-databasen).
/// </summary>
public static class DevSeed
{
    // Dev only: samma lösenord för alla seedade användare, så att inloggningen kan prövas lokalt.
    public const string DevPassword = "dev-password";

    public static readonly Guid TeacherUserId = new("00000000-0000-0000-0000-000000000002");
    public static readonly Guid StudentUserId = new("00000000-0000-0000-0000-000000000003");
    public static readonly Guid SecondStudentUserId = new("00000000-0000-0000-0000-000000000004");

    private static readonly PasswordHasher<User> _passwordHasher = new();

    public static void Seed(AppDb db)
    {
        if (db.Groups.Any())
        {
            return;
        }

        AddExampleData(db);
        db.SaveChanges();
    }

    public static async Task SeedAsync(AppDb db, CancellationToken cancellationToken)
    {
        if (await db.Groups.AnyAsync(cancellationToken))
        {
            return;
        }

        AddExampleData(db);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void AddExampleData(AppDb db)
    {
        var schoolId = PlaceholderIds.SchoolId;
        var teacherId = PlaceholderIds.UserId;

        var group = new Group { SchoolId = schoolId, Name = "NET25", Term = "HT25" };

        // Klassen som en ny kursomgång kan skapas för (Ny kursomgång). Den läser inga kurser än.
        var nextGroup = new Group { SchoolId = schoolId, Name = "NET26", Term = "HT26" };
        var devOps = new Course
        {
            SchoolId = schoolId,
            Title = "DevOps",
            Code = "DEVOPS",
            Description = "Versionshantering, automatiserade tester, CI/CD och driftsättning i molnet.",
            Points = 25,
            StartsOn = new DateOnly(2026, 10, 12),
            EndsOn = new DateOnly(2026, 11, 13),
        };
        var webApps = new Course
        {
            SchoolId = schoolId,
            Title = "Webbapplikationer i C#",
            Code = "WEBB",
            Points = 40,
            StartsOn = new DateOnly(2026, 3, 2),
            EndsOn = new DateOnly(2026, 4, 24),
        };
        var teacher = new User
        {
            Id = TeacherUserId,
            SchoolId = schoolId,
            Name = "Lärare Lärarsson",
            Email = "larare@rebornlms.local",
            Role = UserRole.Teacher
        };
        var student = new User
        {
            Id = StudentUserId,
            SchoolId = schoolId,
            Name = "Elev Elevsson",
            Email = "elev@rebornlms.local",
            Role = UserRole.Student
        };
        var secondStudent = new User
        {
            Id = SecondStudentUserId,
            SchoolId = schoolId,
            Name = "Elev Tvåsson",
            Email = "elev2@rebornlms.local",
            Role = UserRole.Student
        };

        // Bara utveckling: alla seedade användare, även admin från migrationen, har samma lösenord.
        foreach (var user in new[] { teacher, student, secondStudent })
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, DevPassword);
        }

        var admin = db.Users.FirstOrDefault(user => user.Id == PlaceholderIds.UserId);
        if (admin is not null)
        {
            admin.PasswordHash = _passwordHasher.HashPassword(admin, DevPassword);
        }

        db.Groups.AddRange(group, nextGroup);
        db.Courses.AddRange(devOps, webApps);
        db.Users.AddRange(teacher, student, secondStudent);

        var firstWeek = new Module
        {
            CourseId = devOps.Id,
            Title = "Vecka 1 – CI/CD",
            Description = "Bygg och testa automatiskt vid varje ändring.",
            Position = 1
        };
        var secondWeek = new Module
        {
            CourseId = devOps.Id,
            Title = "Vecka 2 – E2E och versions- och releasehantering",
            Position = 2
        };
        db.Modules.AddRange(firstWeek, secondWeek);
        db.GroupCourses.AddRange(
            new GroupCourse { GroupId = group.Id, CourseId = devOps.Id },
            new GroupCourse { GroupId = group.Id, CourseId = webApps.Id });
        db.Enrollments.AddRange(
            new Enrollment { UserId = teacherId, GroupId = group.Id, Role = GroupRole.Teacher },
            new Enrollment { UserId = teacher.Id, GroupId = group.Id, Role = GroupRole.Teacher },
            new Enrollment { UserId = student.Id, GroupId = group.Id, Role = GroupRole.Student },
            new Enrollment { UserId = secondStudent.Id, GroupId = group.Id, Role = GroupRole.Student });

        var news = new Content
        {
            AuthorId = teacherId,
            Type = ContentType.News,
            Title = "Välkommen till NET25",
            BodyMd = "Schemat för veckan ligger under **Kurser**."
        };
        var theory = new Content
        {
            AuthorId = teacherId,
            Type = ContentType.Theory,
            Title = "Vad är CI/CD?",
            BodyMd = "Continuous integration betyder att varje ändring byggs och testas automatiskt."
        };
        var assignment = new Content
        {
            AuthorId = teacherId,
            Type = ContentType.Assignment,
            Title = "Sätt upp en CI-pipeline",
            BodyMd = "Skapa ett GitHub Actions-workflow som bygger och testar projektet.",
            Assignment = new AssignmentSpec
            {
                GradeScale = GradeScale.PassFailDistinction,
                SubmissionModes = [SubmissionMode.Text, SubmissionMode.File]
            }
        };

        var secondNews = new Content
        {
            AuthorId = teacherId,
            Type = ContentType.News,
            Title = "Gästföreläsning om releasehantering",
            BodyMd = "På fredag kommer en gäst och berättar om hur de släpper kod varje dag."
        };
        var tests = new Content
        {
            AuthorId = teacherId,
            Type = ContentType.Assignment,
            Title = "E2E-tester med Playwright",
            BodyMd = "Skriv end-to-end-tester för inloggningen.",
            Assignment = new AssignmentSpec
            {
                GradeScale = GradeScale.PassFailDistinction,
                SubmissionModes = [SubmissionMode.Text]
            }
        };
        var testPlan = new Content
        {
            AuthorId = teacherId,
            Type = ContentType.Assignment,
            Title = "Skriv en testplan",
            BodyMd = "Beskriv vad som ska testas och hur.",
            Assignment = new AssignmentSpec
            {
                GradeScale = GradeScale.PassFailDistinction,
                SubmissionModes = [SubmissionMode.Text]
            }
        };
        var testPlanLink = new ContentLink
        {
            ContentId = testPlan.Id,
            CourseId = devOps.Id,
            ModuleId = firstWeek.Id,
            Position = 3,
            PublishAt = DateTimeOffset.UtcNow.AddDays(-7),
            DueAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        var gradedSubmission = new Submission
        {
            ContentLinkId = testPlanLink.Id,
            StudentId = student.Id,
            Status = SubmissionStatus.Graded,
            BodyMd = "Testplanen ligger i repot.",
            SubmittedAt = DateTimeOffset.UtcNow.AddDays(-3)
        };

        db.Content.AddRange(news, secondNews, theory, assignment, tests, testPlan);
        db.ContentLinks.Add(testPlanLink);
        db.ContentLinks.Add(new ContentLink { ContentId = secondNews.Id, GroupId = group.Id });
        db.ContentLinks.Add(new ContentLink
        {
            ContentId = tests.Id,
            CourseId = devOps.Id,
            ModuleId = secondWeek.Id,
            Position = 1,
            DueAt = DateTimeOffset.UtcNow.AddDays(3)
        });
        db.Submissions.Add(gradedSubmission);
        db.Grades.Add(new Grade
        {
            SubmissionId = gradedSubmission.Id,
            GradedById = teacher.Id,
            Level = GradeLevel.Pass,
            Feedback = "Tydlig plan. Lägg gärna till hur ni mäter täckning.",
            GradedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        var assignmentLink = new ContentLink
        {
            ContentId = assignment.Id,
            GroupId = group.Id,
            CourseId = devOps.Id,
            ModuleId = firstWeek.Id,
            Position = 2,
            DueAt = DateTimeOffset.UtcNow.AddDays(14)
        };
        db.ContentLinks.AddRange(
            new ContentLink { ContentId = news.Id, GroupId = group.Id, IsPinned = true },
            new ContentLink
            {
                ContentId = theory.Id,
                CourseId = devOps.Id,
                ModuleId = firstWeek.Id,
                Position = 1
            },
            assignmentLink);

        // Elevvyn: den första teorin är klarmarkerad, och i vecka 2 ligger en teori som kan göras nu
        // och en som är låst tills den är klar. Då syns "Klart", "Nästa steg" och "Låses upp härnäst".
        var theoryLink = db.ContentLinks.Local.Single(link => link.ContentId == theory.Id);
        var versioning = new Content
        {
            AuthorId = teacherId,
            Type = ContentType.Theory,
            Title = "Semantisk versionering och releaser",
            BodyMd = "MAJOR.MINOR.PATCH: när ska vilket tal höjas?"
        };
        var changelog = new Content
        {
            AuthorId = teacherId,
            Type = ContentType.Theory,
            Title = "Release notes och changelog",
            BodyMd = "Så skriver du en changelog som andra faktiskt läser."
        };
        var versioningLink = new ContentLink
        {
            ContentId = versioning.Id,
            CourseId = devOps.Id,
            ModuleId = secondWeek.Id,
            Position = 2
        };
        var changelogLink = new ContentLink
        {
            ContentId = changelog.Id,
            CourseId = devOps.Id,
            ModuleId = secondWeek.Id,
            Position = 3
        };
        db.Content.AddRange(versioning, changelog);
        db.ContentLinks.AddRange(versioningLink, changelogLink);
        db.ContentLinkDependencies.Add(
            new ContentLinkDependency { BlockedLinkId = changelogLink.Id, BlockingLinkId = versioningLink.Id });
        db.PartCompletions.Add(new PartCompletion
        {
            ContentLinkId = theoryLink.Id,
            StudentId = student.Id,
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        // Ena eleven har lämnat in, så att veckolistan visar 1/2 inlämnade.
        db.Submissions.AddRange(
            new Submission
            {
                ContentLinkId = assignmentLink.Id,
                StudentId = student.Id,
                Status = SubmissionStatus.Submitted,
                BodyMd = "Workflowet ligger i .github/workflows/ci.yml.",
                SubmittedAt = DateTimeOffset.UtcNow.AddHours(-3)
            },
            new Submission { ContentLinkId = assignmentLink.Id, StudentId = secondStudent.Id });

        // Notiser till Elev Elevsson: en oläst nyhet, ett oläst betyg och tre lästa. Nyheterna och uppgifterna
        // har en notis redan här, så att notisjobbet inte skapar nya för dem när servern startar.
        var now = DateTimeOffset.UtcNow;
        db.Notifications.AddRange(
            new Notification
            {
                UserId = student.Id,
                Kind = NotificationKind.NewsPublished,
                ContentId = news.Id,
                CreatedAt = now.AddHours(-2)
            },
            new Notification
            {
                UserId = student.Id,
                Kind = NotificationKind.SubmissionGraded,
                ContentId = testPlan.Id,
                SubmissionId = gradedSubmission.Id,
                CreatedAt = now.AddDays(-1)
            },
            new Notification
            {
                UserId = student.Id,
                Kind = NotificationKind.NewsPublished,
                ContentId = secondNews.Id,
                CreatedAt = now.AddDays(-1).AddHours(-3),
                ReadAt = now.AddDays(-1)
            },
            new Notification
            {
                UserId = student.Id,
                Kind = NotificationKind.AssignmentPublished,
                ContentId = assignment.Id,
                CreatedAt = now.AddDays(-2),
                ReadAt = now.AddDays(-2).AddHours(1)
            },
            new Notification
            {
                UserId = student.Id,
                Kind = NotificationKind.AssignmentPublished,
                ContentId = tests.Id,
                CreatedAt = now.AddDays(-2).AddHours(-1),
                ReadAt = now.AddDays(-2).AddHours(1)
            });
    }
}
