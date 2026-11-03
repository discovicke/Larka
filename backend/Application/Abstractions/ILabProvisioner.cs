using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

/// <summary>En elev som ska få ett repo, och inlämningen repot hör till.</summary>
public record RepoRequest(Guid SubmissionId, User Student);

/// <summary>
/// Ett repo tillsammans med elevens skola. Check-refresh behöver skolans installation, och StudentRepo
/// självt har ingen SchoolId.
/// </summary>
public record StudentRepoContext(StudentRepo Repo, Guid SchoolId);

/// <summary>
/// En pending repo-rad med eleven som ska bjudas in när GitHub-repot har skapats. För grupprepon
/// innehåller GroupMembers alla elever som ska bjudas in.
/// </summary>
public record RepoProvisioningContext(
    StudentRepo Repo, User Student, IReadOnlyList<User>? GroupMembers = null);

/// <summary>
/// Resultatet av en deadline-låsning. Locked är sant när repot arkiverades. OwnCommit är sant när eleven har
/// en egen commit före deadline; bara då är Sha en inlämning. Sha är null utan egen commit.
/// </summary>
public record RepoLockResult(
    bool Locked, string? Sha, DateTimeOffset? CommitAt, string? Error, bool OwnCommit = false);

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

    /// <summary>
    /// Låser repot vid deadline när eleven har en egen commit före deadline och sparar den som inlämning.
    /// Utan egen commit lämnas repot öppet, om inte lockWithoutOwnCommit är satt (ingen deadline återstår).
    /// </summary>
    Task<RepoLockResult> LockAtDeadlineAsync(
        StudentRepoContext context,
        DateTimeOffset deadline,
        bool lockWithoutOwnCommit,
        CancellationToken ct);

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

    /// <summary>Lägger till en elev som collaborator på ett enskilt grupprepo.</summary>
    Task AddRepoCollaboratorAsync(
        StudentRepo repo, Guid schoolId, User student, CancellationToken ct);

    /// <summary>Tar bort en elev som collaborator från ett enskilt grupprepo.</summary>
    Task RemoveRepoCollaboratorAsync(
        StudentRepo repo, Guid schoolId, User student, CancellationToken ct);
}
