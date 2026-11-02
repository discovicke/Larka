using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ISchoolRepository
{
    Task<School?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);

    /// <summary>Skolan som är kopplad till tenanten i Microsoft Entra ID.</summary>
    Task<School?> GetByEntraTenantAsync(string tenantId, CancellationToken ct);

    /// <summary>Skolan som migrationen lägger in. En ny installation har bara den.</summary>
    Task<School?> GetDefaultAsync(CancellationToken ct);
    void Add(School school);
}
