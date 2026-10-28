using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

// Inlämningar hämtas med filer, repo och betyg.
public interface ISubmissionRepository
{
    Task<Submission?> GetAsync(Guid id, CancellationToken ct);
    Task<Submission?> GetForStudentAsync(Guid linkId, Guid studentId, CancellationToken ct);

    /// <summary>Elevens inlämningar på de länkarna, med betyg. Används av elevvyn.</summary>
    Task<List<Submission>> GetForStudentAsync(
        Guid studentId, IReadOnlyCollection<Guid> linkIds, CancellationToken ct);

    /// <summary>
    /// Elevens senaste inlämningar (inte de som bara är utdelade), med betyg, de senaste först.
    /// </summary>
    Task<List<Submission>> GetRecentForStudentAsync(Guid studentId, int take, CancellationToken ct);

    Task<List<Submission>> GetByLinkAsync(Guid linkId, CancellationToken ct);

    // Lärare på en grupp som utdelningen gäller för, eller på en grupp som läser kursen den gäller för.
    Task<bool> IsTeacherOfLinkAsync(Guid linkId, Guid userId, CancellationToken ct);
    void Add(Submission submission);
    void AddRange(IEnumerable<Submission> submissions);
    void Remove(Submission submission);
    void AddFiles(IEnumerable<SubmissionFile> files);
    void RemoveFiles(IEnumerable<SubmissionFile> files);
    void AddGrade(Grade grade);
    Task<List<StudentRepo>> GetAllReposAsync(CancellationToken ct);
}
