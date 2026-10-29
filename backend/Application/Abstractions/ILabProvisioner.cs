using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

/// <summary>En elev som ska få ett repo, och inlämningen repot hör till.</summary>
public record RepoRequest(Guid SubmissionId, User Student);

/// <summary>
/// Ett repo tillsammans med elevens skola. Check-refresh behöver skolans installation, och StudentRepo
/// självt har ingen SchoolId.
/// </summary>
public record StudentRepoContext(StudentRepo Repo, Guid SchoolId);

public interface ILabProvisioner
{
    // Skapar ett repo per elev från mallen (templateRepo = "org/mall").
    Task<IReadOnlyList<StudentRepo>> ProvisionAsync(
        string templateRepo, string assignmentTitle, IReadOnlyList<RepoRequest> requests, CancellationToken ct);

    Task RefreshCheckStatusAsync(
        IReadOnlyList<StudentRepoContext> repos, CancellationToken ct); // pollar checks -> Green/Red

    /// <summary>
    /// Bjuder in en elev som precis kopplat GitHub till sina redan skapade repon. Får aldrig fälla
    /// själva kontokopplingen; GitHub-fel loggas i stället.
    /// </summary>
    Task InviteToExistingReposAsync(User student, CancellationToken ct);
}
