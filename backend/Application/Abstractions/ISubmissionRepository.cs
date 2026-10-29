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
