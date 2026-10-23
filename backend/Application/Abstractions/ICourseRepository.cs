using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ICourseRepository
{
    Task<List<Course>> GetAllAsync(CancellationToken ct);
    Task<Course?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid schoolId, string code, CancellationToken ct);
    void Add(Course course);
}
