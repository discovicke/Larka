using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// Elevens GitHub-repo för en uppgift i repo-läge (Classroom-ersättare, Fake nu / Octokit sen).
public class StudentRepo
{
    public Guid SubmissionId { get; set; }

    // Satt när repot är gruppens gemensamma repo. SubmissionId är då gruppens ledarinlämning.
    public Guid? AssignmentGroupId { get; set; }
    public AssignmentGroup? Group { get; set; }

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

    // Baseline när repot skapades från mallen. Används för att skilja "inte startat" från "har börjat".
    public string? InitialSha { get; set; }
    public DateTimeOffset? InitialCommitAt { get; set; }
    public DateTimeOffset? ProvisionedAt { get; set; }

    public string? LastSha { get; set; }
    public string? LastCommitMessage { get; set; }
    public DateTimeOffset? LastCommitAt { get; set; }
    public int CommitCount { get; set; }
    public CheckStatus CheckStatus { get; set; } = CheckStatus.Pending;
    public DateTimeOffset? CheckStatusChangedAt { get; set; }
    public int ChecksPassed { get; set; }
    public int ChecksTotal { get; set; }

    // Sätts när deadlinen har passerat och repot låsts. SubmissionSha är den commit som räknas.
    public string? SubmissionSha { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public string? LockError { get; set; }

    // Sätts när läraren begär komplettering och repot öppnas för nya commits. Hindrar deadlinejobbet
    // från att låsa repot igen direkt efter att deadlinen redan passerat.
    public DateTimeOffset? ReopenedAt { get; set; }

    public List<StudentRepoCheckRun> CheckRuns { get; set; } = [];

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
