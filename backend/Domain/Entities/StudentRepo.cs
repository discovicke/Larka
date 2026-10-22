using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// Elevens GitHub-repo för en uppgift i repo-läge (Classroom-ersättare, Fake nu / Octokit sen).
public class StudentRepo
{
    public Guid SubmissionId { get; set; }
    public string RepoFullName { get; set; } = "";
    public string? LastSha { get; set; }
    public CheckStatus CheckStatus { get; set; } = CheckStatus.Pending;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
