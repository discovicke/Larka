using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;

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

    public static async Task<Group> AddGroupAsync(AppDb db, Guid? schoolId = null)
    {
        var group = new Group
        {
            SchoolId = schoolId ?? (await AddSchoolAsync(db)).Id,
            Name = $"NET{Guid.NewGuid():N}"[..10],
            Term = "HT26"
        };
        db.Groups.Add(group);
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

    /// <summary>Skapar en kurs. Skickas groupId med läser den gruppen kursen (GroupCourse).</summary>
    public static async Task<Course> AddCourseAsync(AppDb db, Guid schoolId, Guid? groupId = null)
    {
        var course = new Course { SchoolId = schoolId, Title = "Kurs", Code = $"K-{Guid.NewGuid():N}"[..10] };
        db.Courses.Add(course);
        if (groupId is not null)
        {
            db.GroupCourses.Add(new GroupCourse { GroupId = groupId.Value, CourseId = course.Id });
        }

        await db.SaveChangesAsync();
        return course;
    }
}
