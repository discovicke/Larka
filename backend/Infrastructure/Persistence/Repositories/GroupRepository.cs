using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class GroupRepository(AppDb db) : IGroupRepository
{
    public Task<List<Group>> GetAllAsync(CancellationToken ct) => db.Groups.ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid groupId, CancellationToken ct) =>
        db.Groups.AnyAsync(g => g.Id == groupId, ct);

    public Task<bool> CourseExistsAsync(Guid courseId, CancellationToken ct) =>
        db.Courses.AnyAsync(c => c.Id == courseId, ct);

    public async Task<List<Course>> GetCoursesAsync(Guid groupId, CancellationToken ct)
    {
        var courseIds = await db.GroupCourses
            .Where(g => g.GroupId == groupId)
            .Select(g => g.CourseId)
            .ToListAsync(ct);
        return await db.Courses.Where(c => courseIds.Contains(c.Id)).ToListAsync(ct);
    }

    public Task<List<Guid>> GetGroupIdsReadingCourseAsync(Guid courseId, CancellationToken ct) =>
        db.GroupCourses.Where(gc => gc.CourseId == courseId).Select(gc => gc.GroupId).ToListAsync(ct);

    public Task<bool> ReadsCourseAsync(Guid groupId, Guid courseId, CancellationToken ct) =>
        db.GroupCourses.AnyAsync(g => g.GroupId == groupId && g.CourseId == courseId, ct);

    public void AddCourse(GroupCourse groupCourse) => db.GroupCourses.Add(groupCourse);
}
