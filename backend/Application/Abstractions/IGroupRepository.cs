using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface IGroupRepository
{
    Task<List<Group>> GetAllAsync(CancellationToken ct);
    Task<Group?> GetAsync(Guid groupId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid groupId, CancellationToken ct);
    Task<bool> NameExistsAsync(Guid schoolId, string name, string term, CancellationToken ct);
    Task<bool> CourseExistsAsync(Guid courseId, CancellationToken ct);
    Task<List<Course>> GetCoursesAsync(Guid groupId, CancellationToken ct);
    Task<List<Guid>> GetGroupIdsReadingCourseAsync(Guid courseId, CancellationToken ct);
    Task<bool> ReadsCourseAsync(Guid groupId, Guid courseId, CancellationToken ct);
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    void Add(Group group);
    void AddCourse(GroupCourse groupCourse);
    void AddMember(Enrollment enrollment);
}
