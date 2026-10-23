using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class GroupRepository(AppDb db) : IGroupRepository
{
    public Task<List<Group>> GetAllAsync(CancellationToken ct) => db.Groups.ToListAsync(ct);

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

    public void Add(Group group) => db.Groups.Add(group);

    public void AddCourse(GroupCourse groupCourse) => db.GroupCourses.Add(groupCourse);

    public void AddMember(Enrollment enrollment) => db.Enrollments.Add(enrollment);
}
