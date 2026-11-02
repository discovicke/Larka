using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ISchoolRepository
{
    Task<School?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);

    /// <summary>Skolan som är kopplad till tenanten i Microsoft Entra ID.</summary>
    Task<School?> GetByEntraTenantAsync(string tenantId, CancellationToken ct);
    void Add(School school);
}
