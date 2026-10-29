using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ICourseRepository
{
    Task<Course?> GetAsync(Guid id, CancellationToken ct);
    Task<List<Course>> GetForSchoolAsync(Guid schoolId, CancellationToken ct);
    Task<List<Course>> GetByIdsAsync(IReadOnlyCollection<Guid> courseIds, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid schoolId, string code, CancellationToken ct);
    void Add(Course course);
    void Remove(Course course);
}
