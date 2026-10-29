using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ICourseRepository
{
    Task<Course?> GetAsync(Guid id, CancellationToken ct);
    Task<List<Course>> GetForSchoolAsync(Guid schoolId, CancellationToken ct);
    /// <summary>Kurserna i skolan som någon av användarens grupper läser, vilken roll användaren än har.</summary>
    Task<List<Course>> GetForMemberAsync(Guid schoolId, Guid userId, CancellationToken ct);
    Task<List<Course>> GetByIdsAsync(IReadOnlyCollection<Guid> courseIds, CancellationToken ct);
    Task<bool> CodeExistsAsync(Guid schoolId, string code, CancellationToken ct);
    void Add(Course course);
    void Remove(Course course);
}
