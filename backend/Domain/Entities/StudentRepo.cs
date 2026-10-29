using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// Elevens GitHub-repo för en uppgift i repo-läge (Classroom-ersättare, Fake nu / Octokit sen).
public class StudentRepo
{
    public Guid SubmissionId { get; set; }

    // Tom tills GitHub-repot är skapat. Provisioning sker i bakgrunden så att publicering inte blockeras.
    public string RepoFullName { get; set; } = "";

    // Snapshot från utdelningen, så att en senare ändring av uppgiften inte påverkar en pågående retry.
    public string? TemplateRepo { get; set; }
    public string? AssignmentTitle { get; set; }
    public string? RepoNamePattern { get; set; }
    public string? DesiredRepoName { get; set; }

    public RepoProvisioningState ProvisioningState { get; set; } = RepoProvisioningState.Pending;
    public string? ProvisioningError { get; set; }
    public int ProvisioningAttempts { get; set; }
    public DateTimeOffset? NextProvisioningAttemptAt { get; set; }

    public string? LastSha { get; set; }
    public CheckStatus CheckStatus { get; set; } = CheckStatus.Pending;

    // Sätts när deadlinen har passerat och repot låsts. SubmissionSha är den commit som räknas.
    public string? SubmissionSha { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public string? LockError { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
