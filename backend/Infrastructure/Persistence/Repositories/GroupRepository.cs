using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class GroupRepository(AppDb db) : IGroupRepository
{
    public Task<List<Group>> GetForSchoolAsync(Guid schoolId, CancellationToken ct) =>
        db.Groups.Where(group => group.SchoolId == schoolId).ToListAsync(ct);

    public Task<List<Group>> GetForMemberAsync(Guid schoolId, Guid userId, CancellationToken ct) =>
        db.Groups
            .Where(group => group.SchoolId == schoolId
                && (db.Enrollments.Any(enrollment => enrollment.GroupId == group.Id && enrollment.UserId == userId)
                    || db.GroupCourses.Any(groupCourse => groupCourse.GroupId == group.Id
                        && db.CourseTeachers.Any(courseTeacher =>
                            courseTeacher.CourseId == groupCourse.CourseId && courseTeacher.UserId == userId))))
            .ToListAsync(ct);

    public Task<Dictionary<Guid, int>> GetMemberCountsAsync(Guid schoolId, CancellationToken ct) =>
        db.Enrollments
            .Where(enrollment => db.Groups.Any(group => group.Id == enrollment.GroupId && group.SchoolId == schoolId))
            .GroupBy(enrollment => enrollment.GroupId)
            .Select(grouped => new { GroupId = grouped.Key, Count = grouped.Count() })
            .ToDictionaryAsync(row => row.GroupId, row => row.Count, ct);

    public async Task<Dictionary<Guid, DateOnly>> GetLastCourseEndsAsync(Guid schoolId, CancellationToken ct)
    {
        var rows = await db.GroupCourses
            .Where(groupCourse => db.Groups.Any(owner => owner.Id == groupCourse.GroupId && owner.SchoolId == schoolId))
            .Join(db.Courses, groupCourse => groupCourse.CourseId, course => course.Id,
                (groupCourse, course) => new { groupCourse.GroupId, course.EndsOn })
            .ToListAsync(ct);
        return rows
            .GroupBy(row => row.GroupId)
            .Where(grouped => grouped.All(row => row.EndsOn is not null))
            .ToDictionary(grouped => grouped.Key, grouped => grouped.Max(row => row.EndsOn!.Value));
    }

    public Task<int> CountMembersAsync(Guid groupId, CancellationToken ct) =>
        db.Enrollments.CountAsync(enrollment => enrollment.GroupId == groupId, ct);

    public Task<Group?> GetAsync(Guid groupId, CancellationToken ct) =>
        db.Groups.FirstOrDefaultAsync(group => group.Id == groupId, ct);

    public Task<bool> ExistsAsync(Guid groupId, CancellationToken ct) =>
        db.Groups.AnyAsync(group => group.Id == groupId, ct);

    public Task<bool> NameExistsAsync(Guid schoolId, string name, string term, CancellationToken ct) =>
        db.Groups.AnyAsync(
            group => group.SchoolId == schoolId && group.Name == name && group.Term == term, ct);

    public Task<bool> CourseExistsAsync(Guid courseId, CancellationToken ct) =>
        db.Courses.AnyAsync(course => course.Id == courseId, ct);

    public async Task<List<Course>> GetCoursesAsync(Guid groupId, CancellationToken ct)
    {
        var courseIds = await db.GroupCourses
            .Where(groupCourse => groupCourse.GroupId == groupId)
            .Select(groupCourse => groupCourse.CourseId)
            .ToListAsync(ct);
        return await db.Courses.Where(course => courseIds.Contains(course.Id)).ToListAsync(ct);
    }

    public Task<List<Guid>> GetGroupIdsReadingCourseAsync(Guid courseId, CancellationToken ct) =>
        db.GroupCourses
            .Where(groupCourse => groupCourse.CourseId == courseId)
            .Select(groupCourse => groupCourse.GroupId)
            .ToListAsync(ct);

    public Task<bool> ReadsCourseAsync(Guid groupId, Guid courseId, CancellationToken ct) =>
        db.GroupCourses.AnyAsync(
            groupCourse => groupCourse.GroupId == groupId && groupCourse.CourseId == courseId, ct);

    public Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.Enrollments.AnyAsync(enrollment => enrollment.GroupId == groupId && enrollment.UserId == userId, ct);

    public Task<bool> IsTeacherAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.Enrollments.AnyAsync(
            enrollment => enrollment.GroupId == groupId
                && enrollment.UserId == userId
                && enrollment.Role == GroupRole.Teacher,
            ct);

    public Task<bool> IsTeacherOfCourseAsync(Guid courseId, Guid userId, CancellationToken ct) =>
        db.CourseTeachers.AnyAsync(
            courseTeacher => courseTeacher.CourseId == courseId && courseTeacher.UserId == userId, ct);

    public async Task<bool> TeachesAnythingAsync(Guid userId, CancellationToken ct) =>
        await db.CourseTeachers.AnyAsync(courseTeacher => courseTeacher.UserId == userId, ct)
        || await db.Enrollments.AnyAsync(
            enrollment => enrollment.UserId == userId && enrollment.Role == GroupRole.Teacher, ct);

    public Task<List<Enrollment>> GetEnrollmentsForSchoolAsync(Guid schoolId, CancellationToken ct) =>
        db.Enrollments
            .Where(enrollment => db.Groups.Any(group => group.Id == enrollment.GroupId && group.SchoolId == schoolId))
            .ToListAsync(ct);

    public Task<List<GroupCourse>> GetGroupCoursesForSchoolAsync(Guid schoolId, CancellationToken ct) =>
        db.GroupCourses
            .Where(groupCourse => db.Groups.Any(group => group.Id == groupCourse.GroupId && group.SchoolId == schoolId))
            .ToListAsync(ct);

    public Task<bool> TeachesCourseOfGroupAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupCourses.AnyAsync(
            groupCourse => groupCourse.GroupId == groupId
                && db.CourseTeachers.Any(courseTeacher =>
                    courseTeacher.CourseId == groupCourse.CourseId && courseTeacher.UserId == userId),
            ct);

    public Task<List<string>> GetGroupNamesReadingCourseAsync(Guid courseId, CancellationToken ct) =>
        db.GroupCourses
            .Where(groupCourse => groupCourse.CourseId == courseId)
            .Join(db.Groups, groupCourse => groupCourse.GroupId, group => group.Id, (_, group) => group.Name)
            .OrderBy(name => name)
            .ToListAsync(ct);

    public async Task<Dictionary<Guid, List<string>>> GetOtherGroupNamesReadingCoursesAsync(
        IReadOnlyCollection<Guid> courseIds, Guid exceptGroupId, CancellationToken ct)
    {
        var rows = await db.GroupCourses
            .Where(groupCourse => courseIds.Contains(groupCourse.CourseId) && groupCourse.GroupId != exceptGroupId)
            .Join(db.Groups, groupCourse => groupCourse.GroupId, group => group.Id,
                (groupCourse, group) => new { groupCourse.CourseId, group.Name })
            .OrderBy(row => row.Name)
            .ToListAsync(ct);
        return rows
            .GroupBy(row => row.CourseId)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Select(row => row.Name).ToList());
    }

    // Lärare i en kurs är den som står i CourseTeachers. Lärarrollen i en klass räknas inte här, bara eleven,
    // och läraren behöver inte vara medlem i någon klass som läser kursen.
    public async Task<List<CourseRole>> GetCourseRolesAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.Enrollments
            .Where(enrollment => enrollment.UserId == userId && enrollment.Role != GroupRole.Teacher)
            .Join(
                db.GroupCourses,
                enrollment => enrollment.GroupId,
                groupCourse => groupCourse.GroupId,
                (enrollment, groupCourse) => new { groupCourse.CourseId, enrollment.Role })
            .ToListAsync(ct);
        var taught = await db.CourseTeachers
            .Where(courseTeacher => courseTeacher.UserId == userId)
            .Select(courseTeacher => courseTeacher.CourseId)
            .ToListAsync(ct);

        return rows
            .Concat(taught.Select(courseId => new { CourseId = courseId, Role = GroupRole.Teacher }))
            .GroupBy(row => row.CourseId)
            .Select(group => new CourseRole(group.Key, Strongest(group.Select(row => row.Role))))
            .ToList();
    }

    public Task<List<GroupRef>> GetMemberGroupsAsync(Guid userId, CancellationToken ct) =>
        db.Enrollments
            .Where(enrollment => enrollment.UserId == userId)
            .Join(db.Groups, enrollment => enrollment.GroupId, group => group.Id, (_, group) => group)
            .OrderBy(group => group.Name)
            .Select(group => new GroupRef(group.Id, group.Name))
            .ToListAsync(ct);

    public Task<List<Guid>> GetStudentGroupIdsAsync(Guid userId, CancellationToken ct) =>
        db.Enrollments
            .Where(enrollment => enrollment.UserId == userId && enrollment.Role == GroupRole.Student)
            .Select(enrollment => enrollment.GroupId)
            .ToListAsync(ct);

    public async Task<List<StudentCourse>> GetStudentCoursesAsync(Guid userId, CancellationToken ct)
    {
        var studentGroupIds = db.Enrollments
            .Where(enrollment => enrollment.UserId == userId && enrollment.Role == GroupRole.Student)
            .Select(enrollment => enrollment.GroupId);

        var rows = await db.GroupCourses
            .Where(groupCourse => studentGroupIds.Contains(groupCourse.GroupId))
            .Join(
                db.Courses,
                groupCourse => groupCourse.CourseId,
                course => course.Id,
                (groupCourse, course) => new { Course = course, groupCourse.GroupId })
            .ToListAsync(ct);

        return rows
            .GroupBy(row => row.Course.Id)
            .Select(group => new StudentCourse(
                group.First().Course,
                group.Select(row => row.GroupId).Distinct().ToList()))
            .ToList();
    }

    public async Task<List<CourseStudent>> GetStudentsByCourseAsync(
        IReadOnlyCollection<Guid> courseIds, CancellationToken ct) =>
        await (from groupCourse in db.GroupCourses
               join enrollment in db.Enrollments on groupCourse.GroupId equals enrollment.GroupId
               where courseIds.Contains(groupCourse.CourseId) && enrollment.Role == GroupRole.Student
               select new CourseStudent(groupCourse.CourseId, enrollment.UserId))
            .Distinct()
            .ToListAsync(ct);

    public Task<List<GroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken ct) =>
        db.Enrollments
            .Where(enrollment => enrollment.GroupId == groupId)
            .Join(
                db.Users.Where(user => user.DeactivatedAt == null),
                enrollment => enrollment.UserId,
                user => user.Id,
                (enrollment, user) => new { UserId = user.Id, user.Name, enrollment.Role })
            .OrderBy(row => row.Name)
            .Select(row => new GroupMemberDto(row.UserId, row.Name, row.Role))
            .ToListAsync(ct);

    // Lärare går före elev.
    private static GroupRole Strongest(IEnumerable<GroupRole> roles) =>
        roles.OrderByDescending(Rank).First();

    private static int Rank(GroupRole role) => role switch
    {
        GroupRole.Teacher => 2,
        _ => 1
    };

    public void Add(Group group) => db.Groups.Add(group);

    public void AddCourse(GroupCourse groupCourse) => db.GroupCourses.Add(groupCourse);

    public void AddMember(Enrollment enrollment) => db.Enrollments.Add(enrollment);

    public Task<Enrollment?> GetMemberAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.Enrollments.FirstOrDefaultAsync(
            enrollment => enrollment.GroupId == groupId && enrollment.UserId == userId, ct);

    public Task<bool> HasContentAsync(Guid groupId, Guid? courseId, CancellationToken ct) =>
        db.ContentLinks.AnyAsync(
            link => link.GroupId == groupId && (courseId == null || link.CourseId == courseId), ct);

    public void RemoveMember(Enrollment enrollment) => db.Enrollments.Remove(enrollment);

    public async Task RemoveCourseAsync(Guid groupId, Guid courseId, CancellationToken ct) =>
        db.GroupCourses.RemoveRange(await db.GroupCourses
            .Where(groupCourse => groupCourse.GroupId == groupId && groupCourse.CourseId == courseId)
            .ToListAsync(ct));

    public async Task RemoveAsync(Group group, CancellationToken ct)
    {
        db.Enrollments.RemoveRange(
            await db.Enrollments.Where(enrollment => enrollment.GroupId == group.Id).ToListAsync(ct));
        db.GroupCourses.RemoveRange(
            await db.GroupCourses.Where(groupCourse => groupCourse.GroupId == group.Id).ToListAsync(ct));
        db.Groups.Remove(group);
    }
}
