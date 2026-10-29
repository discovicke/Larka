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

    public Task<bool> IsTeacherOfCourseAsync(Guid courseId, Guid userId, CancellationToken ct)
    {
        var teacherGroupIds = db.Enrollments
            .Where(enrollment => enrollment.UserId == userId && enrollment.Role == GroupRole.Teacher)
            .Select(enrollment => enrollment.GroupId);

        return db.GroupCourses.AnyAsync(
            groupCourse => groupCourse.CourseId == courseId && teacherGroupIds.Contains(groupCourse.GroupId), ct);
    }

    public Task<List<GroupTeacher>> GetTeachersAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct) =>
        db.Enrollments
            .Where(enrollment => groupIds.Contains(enrollment.GroupId) && enrollment.Role == GroupRole.Teacher)
            .Join(
                db.Users,
                enrollment => enrollment.UserId,
                user => user.Id,
                (enrollment, user) => new { enrollment.GroupId, UserId = user.Id, user.Name, user.Email })
            .Join(
                db.Groups,
                row => row.GroupId,
                group => group.Id,
                (row, group) => new GroupTeacher(row.UserId, row.Name, row.Email, group.Name))
            .ToListAsync(ct);

    public async Task<List<CourseRole>> GetCourseRolesAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.Enrollments
            .Where(enrollment => enrollment.UserId == userId)
            .Join(
                db.GroupCourses,
                enrollment => enrollment.GroupId,
                groupCourse => groupCourse.GroupId,
                (enrollment, groupCourse) => new { groupCourse.CourseId, enrollment.Role })
            .ToListAsync(ct);

        return rows
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
                db.Users,
                enrollment => enrollment.UserId,
                user => user.Id,
                (enrollment, user) => new { UserId = user.Id, user.Name, enrollment.Role })
            .OrderBy(row => row.Name)
            .Select(row => new GroupMemberDto(row.UserId, row.Name, row.Role))
            .ToListAsync(ct);

    // Lärare går före mentor, som går före elev.
    private static GroupRole Strongest(IEnumerable<GroupRole> roles) =>
        roles.OrderByDescending(Rank).First();

    private static int Rank(GroupRole role) => role switch
    {
        GroupRole.Teacher => 3,
        GroupRole.Mentor => 2,
        _ => 1
    };

    public void Add(Group group) => db.Groups.Add(group);

    public void AddCourse(GroupCourse groupCourse) => db.GroupCourses.Add(groupCourse);

    public void AddMember(Enrollment enrollment) => db.Enrollments.Add(enrollment);
}
