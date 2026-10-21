using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface ILabRepository
{
    Task<List<LabAssignment>> GetByCourseAsync(Guid courseId, CancellationToken ct);
    void Add(LabAssignment lab);
    Task<List<StudentLabRepo>> GetReposAsync(Guid labId, CancellationToken ct);
    Task<List<StudentLabRepo>> GetAllReposAsync(CancellationToken ct);
    void AddRepos(IEnumerable<StudentLabRepo> repos);
}
