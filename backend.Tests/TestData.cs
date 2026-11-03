using System.Security.Claims;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

/// <summary>
/// Skapar giltiga rader direkt i databasen. Behövs nu när foreign keys stoppar id:n som inte finns.
/// </summary>
public static class TestData
{
    public static async Task<School> AddSchoolAsync(AppDb db)
    {
        var school = new School { Name = $"Skola {Guid.NewGuid():N}" };
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        return school;
    }

    public static async Task<User> AddUserAsync(AppDb db, Guid? schoolId = null, UserRole role = UserRole.Student)
    {
        var user = new User
        {
            SchoolId = schoolId ?? (await AddSchoolAsync(db)).Id,
            Name = "Test Testsson",
            Email = $"{Guid.NewGuid():N}@example.com",
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Eleven som lämnar in på utdelningen i testerna: en egen elev ("Inlämnare") i gruppen som länken når, så att
    /// gruppens övriga elever inte rörs. Samma elev varje gång, så att en ny inlämning ersätter den förra.
    /// </summary>
    public static async Task<Guid> SubmitterAsync(AppDb db, Guid linkId)
    {
        var link = await db.ContentLinks.SingleAsync(link => link.Id == linkId);
        var groupId = link.GroupId
            ?? await db.GroupCourses
                .Where(groupCourse => groupCourse.CourseId == link.CourseId)
                .OrderBy(groupCourse => groupCourse.GroupId)
                .Select(groupCourse => groupCourse.GroupId)
                .FirstAsync();
        var existing = await (from enrollment in db.Enrollments
                              join user in db.Users on enrollment.UserId equals user.Id
                              where enrollment.GroupId == groupId
                                  && enrollment.Role == GroupRole.Student
                                  && user.Name == _submitterName
                              select (Guid?)user.Id).FirstOrDefaultAsync();
        if (existing is not null)
        {
            return existing.Value;
        }

        var group = await db.Groups.SingleAsync(group => group.Id == groupId);
        var submitter = await AddMemberAsync(db, group);
        submitter.Name = _submitterName;
        await db.SaveChangesAsync();
        return submitter.Id;
    }

    private const string _submitterName = "Inlämnare";

    /// <summary>
    /// Låter en service i scopet köras som användaren, när testet anropar den direkt utan HTTP-förfrågan
    /// (TestCurrentUser läser användaren ur HttpContext).
    /// </summary>
    public static void ActAs(IServiceProvider services, Guid userId) =>
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], "Test"))
        };

    /// <summary>
    /// Skapar en grupp. Med placeholderTeaches är placeholder-admin lärare i gruppen, vilket krävs för att
    /// publicera till bara gruppen (utan kurs). Admin gör inte det av sig själv.
    /// </summary>
    public static async Task<Group> AddGroupAsync(AppDb db, Guid? schoolId = null, bool placeholderTeaches = false)
    {
        var group = new Group
        {
            SchoolId = schoolId ?? (await AddSchoolAsync(db)).Id,
            Name = $"NET{Guid.NewGuid():N}"[..10]
        };
        db.Groups.Add(group);
        if (placeholderTeaches)
        {
            db.Enrollments.Add(new Enrollment
            {
                UserId = PlaceholderIds.UserId,
                GroupId = group.Id,
                Role = GroupRole.Teacher
            });
        }

        await db.SaveChangesAsync();
        return group;
    }

    public static async Task<User> AddMemberAsync(
        AppDb db, Group group, GroupRole role = GroupRole.Student, UserRole userRole = UserRole.Student)
    {
        var user = await AddUserAsync(db, group.SchoolId, userRole);
        db.Enrollments.Add(new Enrollment { UserId = user.Id, GroupId = group.Id, Role = role });
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Gör användaren till lärare i kursen (CourseTeacher), utan att hen blir medlem i någon klass.</summary>
    public static async Task AddCourseTeacherAsync(AppDb db, Course course, User user)
    {
        db.CourseTeachers.Add(new CourseTeacher { CourseId = course.Id, UserId = user.Id });
        await db.SaveChangesAsync();
    }

    /// <summary>Skapar en lärare i kursens skola som är lärare i kursen men inte medlem i någon klass.</summary>
    public static async Task<User> AddCourseTeacherAsync(AppDb db, Course course)
    {
        var user = await AddUserAsync(db, course.SchoolId, UserRole.Teacher);
        await AddCourseTeacherAsync(db, course, user);
        return user;
    }

    /// <summary>
    /// Skapar en kurs. Skickas groupId läser den gruppen kursen (GroupCourse). Testernas standardanvändare är
    /// placeholder-admin, som bara undervisar där hen står i CourseTeachers, så den läggs in som lärare om inte
    /// placeholderTeaches är false. Tester av admin utan lärarroll skickar false.
    /// </summary>
    public static async Task<Course> AddCourseAsync(
        AppDb db, Guid schoolId, Guid? groupId = null, bool placeholderTeaches = true)
    {
        var course = new Course { SchoolId = schoolId, Title = "Kurs", Code = $"K-{Guid.NewGuid():N}"[..10] };
        db.Courses.Add(course);
        if (groupId is not null)
        {
            db.GroupCourses.Add(new GroupCourse { GroupId = groupId.Value, CourseId = course.Id });
        }

        if (placeholderTeaches)
        {
            db.CourseTeachers.Add(new CourseTeacher { CourseId = course.Id, UserId = PlaceholderIds.UserId });
        }

        await db.SaveChangesAsync();
        return course;
    }

    /// <summary>
    /// Låter placeholder-admin undervisa i en kurs i en annan skola, så att hen får skapa innehåll. Gör ingenting
    /// om det redan är gjort.
    /// </summary>
    public static async Task EnsurePlaceholderTeachesAsync(AppDb db)
    {
        if (await db.CourseTeachers.AnyAsync(courseTeacher => courseTeacher.UserId == PlaceholderIds.UserId))
        {
            return;
        }

        var school = await AddSchoolAsync(db);
        await AddCourseAsync(db, school.Id);
    }

    /// <summary>Gör placeholder-admin till lärare i en kurs som skapats via API:et.</summary>
    public static async Task MakePlaceholderTeacherAsync(AppDb db, Guid courseId)
    {
        db.CourseTeachers.Add(new CourseTeacher { CourseId = courseId, UserId = PlaceholderIds.UserId });
        await db.SaveChangesAsync();
    }
}
