using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

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
    /// Skapar en grupp. Med placeholderTeaches är placeholder-admin lärare i gruppen, vilket krävs för att
    /// publicera till bara gruppen (utan kurs). Admin gör inte det av sig själv.
    /// </summary>
    public static async Task<Group> AddGroupAsync(AppDb db, Guid? schoolId = null, bool placeholderTeaches = false)
    {
        var group = new Group
        {
            SchoolId = schoolId ?? (await AddSchoolAsync(db)).Id,
            Name = $"NET{Guid.NewGuid():N}"[..10],
            Term = "HT26"
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
