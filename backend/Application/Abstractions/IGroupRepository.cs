using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface IGroupRepository
{
    Task<List<Group>> GetAllAsync(CancellationToken ct);
    Task<bool> ExistsAsync(Guid groupId, CancellationToken ct);
    Task<bool> CourseExistsAsync(Guid courseId, CancellationToken ct);
    Task<List<Course>> GetCoursesAsync(Guid groupId, CancellationToken ct);
    Task<List<Guid>> GetGroupIdsReadingCourseAsync(Guid courseId, CancellationToken ct);
    Task<bool> ReadsCourseAsync(Guid groupId, Guid courseId, CancellationToken ct);
    void AddCourse(GroupCourse groupCourse);
}
