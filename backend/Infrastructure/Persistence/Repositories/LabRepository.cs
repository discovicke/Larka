using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class LabRepository : ILabRepository
{
    private readonly AppDb _db;

    public LabRepository(AppDb db) => _db = db;

    public Task<List<LabAssignment>> GetByCourseAsync(Guid courseId, CancellationToken ct) =>
        _db.Labs.Where(l => l.CourseId == courseId).ToListAsync(ct);

    public void Add(LabAssignment lab) => _db.Labs.Add(lab);

    public Task<List<StudentLabRepo>> GetReposAsync(Guid labId, CancellationToken ct) =>
        _db.LabRepos.Where(r => r.LabId == labId).ToListAsync(ct);

    public Task<List<StudentLabRepo>> GetAllReposAsync(CancellationToken ct) => _db.LabRepos.ToListAsync(ct);

    public void AddRepos(IEnumerable<StudentLabRepo> repos) => _db.LabRepos.AddRange(repos);
}
