using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ISchoolRepository
{
    Task<List<School>> GetAllAsync(CancellationToken ct);
    Task<School?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    void Add(School school);
}
