using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

/// <summary>En inlämning som väntar på bedömning, med uppgift, kurs och elev.</summary>
public record SubmissionToGrade(
    Guid SubmissionId,
    Guid LinkId,
    string Title,
    Guid CourseId,
    Guid StudentId,
    string StudentName,
    DateTimeOffset SubmittedAt);

/// <summary>Antal inlämningar att bedöma i en kurs.</summary>
public record CourseToGradeCount(Guid CourseId, int Count);

/// <summary>En påbörjad inlämning på en länk.</summary>
public record ActiveSubmission(Guid LinkId, Guid StudentId);

/// <summary>En elevrepo som ska låsas eftersom länkens deadline har passerat.</summary>
public record RepoLockContext(
    StudentRepo Repo,
    Submission Submission,
    Guid SchoolId,
    DateTimeOffset Deadline);

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

    /// <summary>
    /// Inlämningar med status Submitted i kurserna, äldst först. Bara de första `take` hämtas.
    /// </summary>
    Task<List<SubmissionToGrade>> GetToGradeAsync(
        IReadOnlyCollection<Guid> courseIds, int take, CancellationToken ct);

    /// <summary>Antal inlämningar med status Submitted per kurs.</summary>
    Task<List<CourseToGradeCount>> GetToGradeCountsAsync(
        IReadOnlyCollection<Guid> courseIds, CancellationToken ct);

    /// <summary>Inlämningar som påbörjats på länkarna, en rad per elev och länk.</summary>
    Task<List<ActiveSubmission>> GetActiveForLinksAsync(
        IReadOnlyCollection<Guid> linkIds, CancellationToken ct);

    // Lärare i kursen utdelningen gäller för. Gäller den bara en grupp, utan kurs, lärare i gruppen.
    Task<bool> IsTeacherOfLinkAsync(Guid linkId, Guid userId, CancellationToken ct);
    void Add(Submission submission);
    void AddRange(IEnumerable<Submission> submissions);
    void Remove(Submission submission);
    void AddFiles(IEnumerable<SubmissionFile> files);
    void RemoveFiles(IEnumerable<SubmissionFile> files);
    void AddGrade(Grade grade);
    Task<List<StudentRepoContext>> GetAllReposAsync(CancellationToken ct);

    /// <summary>Pending/failed repon som är redo att försökas igen, med elev och skola.</summary>
    Task<List<RepoProvisioningContext>> GetPendingReposAsync(
        DateTimeOffset now, int take, int maxAttempts, CancellationToken ct);

    /// <summary>Repon vars deadline passerat och som ännu inte låsts.</summary>
    Task<List<RepoLockContext>> GetReposToLockAsync(
        DateTimeOffset now, int take, CancellationToken ct);

    /// <summary>Elevens redan skapade repon. Används när eleven kopplar GitHub efter utdelningen.</summary>
    Task<List<StudentRepo>> GetReposForStudentAsync(Guid studentId, CancellationToken ct);

    /// <summary>Antal elevrepon i skolan, för adminöversikten.</summary>
    Task<int> CountReposForSchoolAsync(Guid schoolId, CancellationToken ct);
}
