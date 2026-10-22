using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

// Inlämningar hämtas med filer, repo och betyg.
public interface ISubmissionRepository
{
    Task<Submission?> GetAsync(Guid id, CancellationToken ct);
    Task<Submission?> GetForStudentAsync(Guid linkId, Guid studentId, CancellationToken ct);
    Task<List<Submission>> GetByLinkAsync(Guid linkId, CancellationToken ct);
    void Add(Submission submission);
    void AddRange(IEnumerable<Submission> submissions);
    void AddFiles(IEnumerable<SubmissionFile> files);
    void RemoveFiles(IEnumerable<SubmissionFile> files);
    void AddGrade(Grade grade);
    Task<List<StudentRepo>> GetAllReposAsync(CancellationToken ct);
}
