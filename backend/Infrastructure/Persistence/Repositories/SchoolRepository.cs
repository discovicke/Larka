using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class SchoolRepository(AppDb db) : ISchoolRepository
{
    public Task<List<School>> GetAllAsync(CancellationToken ct) =>
        db.Schools.OrderBy(school => school.Name).ToListAsync(ct);

    public Task<School?> GetAsync(Guid id, CancellationToken ct) =>
        db.Schools.FirstOrDefaultAsync(school => school.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        db.Schools.AnyAsync(school => school.Id == id, ct);

    public void Add(School school) => db.Schools.Add(school);
}
