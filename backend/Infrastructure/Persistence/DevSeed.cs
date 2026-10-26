using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence;

/// <summary>
/// Exempeldata för lokal utveckling: en klass med två kurser, två veckor i DevOps, en elev och lite innehåll.
/// Körs av EF Core efter Migrate när Database:SeedDevData är true, och bara om det inte finns
/// några grupper än. Placeholder-skolan och placeholder-användaren finns redan via migrationen.
/// </summary>
public static class DevSeed
{
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
        var schoolId = PlaceholderCurrentUser.PlaceholderSchoolId;
        var teacherId = PlaceholderCurrentUser.PlaceholderUserId;

        var group = new Group { SchoolId = schoolId, Name = "NET25", Term = "HT25" };
        var devOps = new Course { SchoolId = schoolId, Title = "DevOps", Code = "DEVOPS" };
        var webApps = new Course { SchoolId = schoolId, Title = "Webbapplikationer i C#", Code = "WEBB" };
        var student = new User
        {
            SchoolId = schoolId,
            Name = "Elev Elevsson",
            Email = "elev@rebornlms.local",
            Role = UserRole.Student
        };

        db.Groups.Add(group);
        db.Courses.AddRange(devOps, webApps);
        db.Users.Add(student);

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
            new Enrollment { UserId = student.Id, GroupId = group.Id, Role = GroupRole.Student });

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
