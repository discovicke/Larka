using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class SchoolRepository(AppDb db) : ISchoolRepository
{
    public Task<School?> GetAsync(Guid id, CancellationToken ct) =>
        db.Schools.FirstOrDefaultAsync(school => school.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        db.Schools.AnyAsync(school => school.Id == id, ct);

    public Task<School?> GetByEntraTenantAsync(string tenantId, CancellationToken ct) =>
        db.Schools.FirstOrDefaultAsync(school => school.EntraTenantId == tenantId, ct);

    public Task<School?> GetDefaultAsync(CancellationToken ct) => GetAsync(PlaceholderIds.SchoolId, ct);

    public void Add(School school) => db.Schools.Add(school);
}
