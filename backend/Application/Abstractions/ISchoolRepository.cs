using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ISchoolRepository
{
    Task<School?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    void Add(School school);
}
