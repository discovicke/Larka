using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence;

/// <summary>
/// Exempeldata för lokal utveckling: en klass med två kurser, två veckor i DevOps, en lärare, två elever
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

        db.Groups.Add(group);
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

        db.Content.AddRange(news, theory, assignment);
        db.ContentLinks.AddRange(
            new ContentLink { ContentId = news.Id, GroupId = group.Id, IsPinned = true },
            new ContentLink
            {
                ContentId = theory.Id,
                CourseId = devOps.Id,
                ModuleId = firstWeek.Id,
                Position = 1
            },
            new ContentLink
            {
                ContentId = assignment.Id,
                GroupId = group.Id,
                CourseId = devOps.Id,
                ModuleId = firstWeek.Id,
                Position = 2,
                DueAt = DateTimeOffset.UtcNow.AddDays(14)
            });
    }
}
