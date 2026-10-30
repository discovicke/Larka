using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

/// <summary>En elev som ska få ett repo, och inlämningen repot hör till.</summary>
public record RepoRequest(Guid SubmissionId, User Student);

/// <summary>
/// Ett repo tillsammans med elevens skola. Check-refresh behöver skolans installation, och StudentRepo
/// självt har ingen SchoolId.
/// </summary>
public record StudentRepoContext(StudentRepo Repo, Guid SchoolId);

/// <summary>En pending repo-rad med eleven som ska bjudas in när GitHub-repot har skapats.</summary>
public record RepoProvisioningContext(StudentRepo Repo, User Student);

/// <summary>Resultatet av en deadline-låsning. Sha är null när eleven saknade commit före deadline.</summary>
public record RepoLockResult(bool Locked, string? Sha, DateTimeOffset? CommitAt, string? Error);

public interface ILabProvisioner
{
    // Skapar ett repo per elev från mallen (templateRepo = "org/mall"). repoNamePattern är valfritt;
    // null använder GitHubRepoNaming.DefaultPattern.
    Task<IReadOnlyList<StudentRepo>> ProvisionAsync(
        string templateRepo,
        string assignmentTitle,
        string? repoNamePattern,
        IReadOnlyList<RepoRequest> requests,
        CancellationToken ct);

    /// <summary>Skapar ett enskilt repo från en pending rad. Används av bakgrundsjobbet.</summary>
    Task<StudentRepo> ProvisionOneAsync(RepoProvisioningContext context, CancellationToken ct);

    /// <summary>Låser repot vid deadline och sparar senaste commit före deadline som inlämning.</summary>
    Task<RepoLockResult> LockAtDeadlineAsync(
        StudentRepoContext context, DateTimeOffset deadline, CancellationToken ct);

    /// <summary>Avarkiverar repot så att eleven kan committa igen efter en begärd komplettering.</summary>
    Task ReopenForResubmissionAsync(StudentRepoContext context, CancellationToken ct);

    Task RefreshCheckStatusAsync(
        IReadOnlyList<StudentRepoContext> repos, CancellationToken ct); // pollar checks -> Green/Red

    /// <summary>
    /// Bjuder in en elev som precis kopplat GitHub till sina redan skapade repon. Får aldrig fälla
    /// själva kontokopplingen; GitHub-fel loggas i stället.
    /// </summary>
    Task InviteToExistingReposAsync(User student, CancellationToken ct);

    /// <summary>
    /// Tar bort elevens collaborator-åtkomst när kontot kopplas från. Repon raderas aldrig, och
    /// GitHub-fel får inte fälla själva frånkopplingen.
    /// </summary>
    Task RevokeAccessAsync(User student, CancellationToken ct);
}
