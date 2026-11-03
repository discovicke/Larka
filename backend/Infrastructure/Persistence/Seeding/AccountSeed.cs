using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Persistence.Seeding;

/// <summary>
/// Inloggningskonton: admin, larare och elev, med lösenordet samma som användarnamnet. Körs vid varje start i
/// alla miljöer, efter migrationerna, och är idempotent: användarna skapas bara om de saknas, lösenorden sätts
/// om varje gång, och klassen, kopplingarna och kursen skapas bara om de saknas. Inget annat rörs, och inget
/// tas bort. Läraren blir lärare i Demo-kurs när kursen skapas. Demo-klass kopplas bara till Demo-kurs, och när demodatan (DemoSeed) finns skapas varken Demo-klass
/// eller Demo-kurs. Lösenorden loggas aldrig. Kontona är offentliga, så prod-data är öppen för den som prövar dem.
/// </summary>
public sealed class AccountSeed(
    AppDb db,
    IPasswordHasher<User> passwordHasher,
    TimeProvider time,
    IOptions<DatabaseOptions> options,
    ILogger<AccountSeed> logger)
{
    public const string ClassName = "Demo-klass";
    public const string CourseTitle = "Demo-kurs";
    public const string CourseCode = "DEMO";

    private static readonly AccountSpec[] _accounts =
    [
        new("admin", "admin", "Demo Admin", UserRole.Admin),
        new("larare", "larare", "Demo Lärare", UserRole.Teacher),
        new("elev", "elev", "Demo Elev", UserRole.Student)
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        var school = await FindSchoolAsync(ct);
        var created = new List<string>();

        var teacher = await EnsureUserAsync(school, _accounts[1], created, ct);
        var student = await EnsureUserAsync(school, _accounts[2], created, ct);
        await EnsureUserAsync(school, _accounts[0], created, ct);

        // Finns demodatan (DemoSeed) är kontona placerade i riktiga klasser, och Demo-klass och Demo-kurs ska inte
        // skapas om. Då räcker det att användarna och lösenorden finns.
        var hasDemoData = options.Value.SeedDemoData
            || await db.Groups.AnyAsync(
                row => row.SchoolId == school.Id && row.Name == DemoSeed.MarkerGroupName, ct);
        if (!hasDemoData)
        {
            await EnsureDemoClassAsync(school, teacher, student, created, ct);
        }

        await db.SaveChangesAsync(ct);

        var summary = created.Count == 0
            ? "inget, allt fanns redan"
            : string.Join(", ", created);
        logger.LogInformation("Inloggningskonton klara i skolan {School}. Skapade: {Created}", school.Name, summary);
    }

    /// <summary>
    /// Demo-klass med läraren och eleven, och Demo-kurs (bara om skolan saknar kurser) som klassen läser. Klassen
    /// kopplas bara till Demo-kurs, aldrig till skolans övriga kurser.
    /// </summary>
    private async Task EnsureDemoClassAsync(
        School school, User teacher, User student, List<string> created, CancellationToken ct)
    {
        var group = await EnsureGroupAsync(school, created, ct);
        await EnsureEnrollmentAsync(teacher, group, GroupRole.Teacher, created, ct);
        await EnsureEnrollmentAsync(student, group, GroupRole.Student, created, ct);

        var demoCourse = await db.Courses.FirstOrDefaultAsync(
            course => course.SchoolId == school.Id && course.Code == CourseCode, ct);
        if (demoCourse is null && !await db.Courses.AnyAsync(course => course.SchoolId == school.Id, ct))
        {
            demoCourse = new Course { SchoolId = school.Id, Title = CourseTitle, Code = CourseCode };
            db.Courses.Add(demoCourse);
            AddDemoCourseContent(demoCourse, teacher, created);
            db.CourseTeachers.Add(new CourseTeacher { CourseId = demoCourse.Id, UserId = teacher.Id });
            created.Add($"{teacher.Email} som lärare i {CourseTitle}");
        }

        if (demoCourse is not null
            && !await db.GroupCourses.AnyAsync(
                link => link.GroupId == group.Id && link.CourseId == demoCourse.Id, ct))
        {
            db.GroupCourses.Add(new GroupCourse { GroupId = group.Id, CourseId = demoCourse.Id });
            created.Add($"koppling {ClassName} till {demoCourse.Title}");
        }
    }

    /// <summary>Placeholder-skolan (finns från migrationen), annars den första skolan, annars en ny Demo-skola.</summary>
    private async Task<School> FindSchoolAsync(CancellationToken ct)
    {
        var school = await db.Schools.FirstOrDefaultAsync(
                         row => row.Id == PlaceholderIds.SchoolId, ct)
                     ?? await db.Schools.OrderBy(row => row.Name).FirstOrDefaultAsync(ct);
        if (school is not null)
        {
            return school;
        }

        school = new School { Name = "Demo-skolan" };
        db.Schools.Add(school);
        return school;
    }

    private async Task<User> EnsureUserAsync(
        School school, AccountSpec account, List<string> created, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(
            row => row.Email.ToLower() == account.Username, ct);
        if (user is null)
        {
            user = new User
            {
                SchoolId = school.Id,
                Name = account.Name,
                Email = account.Username,
                Role = account.Role
            };
            db.Users.Add(user);
            created.Add($"användaren {account.Username}");
        }

        // Lösenordet följer Demo-inställningen vid varje start, så att en ändrad hash inte blir kvar.
        user.PasswordHash = passwordHasher.HashPassword(user, account.Password);
        return user;
    }

    private async Task<Group> EnsureGroupAsync(School school, List<string> created, CancellationToken ct)
    {
        var group = await db.Groups.FirstOrDefaultAsync(
            row => row.SchoolId == school.Id && row.Name == ClassName, ct);
        if (group is not null)
        {
            return group;
        }

        group = new Group { SchoolId = school.Id, Name = ClassName };
        db.Groups.Add(group);
        created.Add($"klassen {ClassName}");
        return group;
    }

    private async Task EnsureEnrollmentAsync(
        User user, Group group, GroupRole role, List<string> created, CancellationToken ct)
    {
        var enrollment = await db.Enrollments.FirstOrDefaultAsync(
            row => row.UserId == user.Id && row.GroupId == group.Id, ct);
        if (enrollment is null)
        {
            db.Enrollments.Add(new Enrollment { UserId = user.Id, GroupId = group.Id, Role = role });
            created.Add($"{user.Email} som {role} i {ClassName}");
            return;
        }

        enrollment.Role = role;
    }

    /// <summary>En vecka med en teorisida, en uppgift med godkänt/underkänt och en nyhet, alla publicerade.</summary>
    private void AddDemoCourseContent(Course course, User teacher, List<string> created)
    {
        var week = new Module { CourseId = course.Id, Title = "Vecka 1", Position = 1 };
        db.Modules.Add(week);

        var news = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.News,
            Title = "Välkommen till demo-kursen",
            BodyMd = "Det här är demokontona. Allt i kursen är påhittat.",
            Status = ContentStatus.Published
        };
        var theory = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.Theory,
            Title = "Så fungerar en kurs",
            BodyMd = "En kurs består av veckor, och varje vecka innehåller material och uppgifter.",
            Status = ContentStatus.Published
        };
        var assignment = new Content
        {
            AuthorId = teacher.Id,
            Type = ContentType.Assignment,
            Title = "Skriv en kort reflektion",
            BodyMd = "Skriv några meningar om vad du lärt dig under veckan.",
            Status = ContentStatus.Published,
            Assignment = new AssignmentSpec
            {
                GradeScale = GradeScale.PassFail,
                SubmissionModes = [SubmissionMode.Text]
            }
        };
        db.Content.AddRange(news, theory, assignment);

        db.ContentLinks.AddRange(
            new ContentLink { ContentId = news.Id, CourseId = course.Id, IsPinned = true },
            new ContentLink { ContentId = theory.Id, CourseId = course.Id, ModuleId = week.Id, Position = 1 },
            new ContentLink
            {
                ContentId = assignment.Id,
                CourseId = course.Id,
                ModuleId = week.Id,
                Position = 2,
                DueAt = time.GetUtcNow().AddDays(14)
            });

        created.Add($"kursen {CourseTitle} ({CourseCode}) med {week.Title}");
    }

    private sealed record AccountSpec(string Username, string Password, string Name, UserRole Role);
}
