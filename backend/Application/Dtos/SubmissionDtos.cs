using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

// --- requests ---

// Filerna kommer med uppladdningen (FileUpload). Utan KeepFileIds ersätter de tidigare filer vid ny inlämning
// (t.ex. efter komplettering). Med KeepFileIds behålls de filerna och resten tas bort, så att eleven kan
// ändra texten eller byta en enda fil utan att skicka om alla.
public record SubmitDto(string? BodyMd = null, List<Guid>? KeepFileIds = null);

// NeedsWork = begär komplettering: bara feedback, inget betyg.
public record GradeSubmissionDto(string Feedback, GradeLevel? Level = null, bool NeedsWork = false);

// --- responses ---

public record SubmissionDto(
    Guid Id,
    Guid ContentLinkId,
    Guid StudentId,
    string StudentName,
    SubmissionStatus Status,
    string? BodyMd,
    DateTimeOffset? SubmittedAt,
    List<SubmissionFileDto> Files,
    StudentRepoDto? Repo,
    GradeDto? Grade,
    StudentSupportDto? Support)
{
    public static SubmissionDto From(
        Submission submission,
        string studentName,
        List<SubmissionFileDto> files,
        StudentSupportDto? support = null)
    {
        var repo = submission.Repo is null
            ? null
            : StudentRepoDto.From(submission.Repo);
        var grade = submission.Grade is null
            ? null
            : GradeDto.From(submission.Grade);
        return new SubmissionDto(
            submission.Id, submission.ContentLinkId, submission.StudentId, studentName, submission.Status,
            submission.BodyMd, submission.SubmittedAt, files, repo, grade, support);
    }
}

/// <summary>Stödsignaler som läraren ser i trackingtabellen. Beräknas alltid från aktuell data.</summary>
public record StudentSupportDto(
    bool NotStarted,
    bool NoRecentPush,
    bool RedChecks,
    bool HelpRequested,
    DateTimeOffset? HelpRequestedAt,
    string? HelpMessage,
    bool HelpResolved);

/// <summary>Url är en tidsbegränsad länk som skapas när filen läses, inte något som sparas.</summary>
public record SubmissionFileDto(Guid Id, string FileName, string Url, long SizeBytes)
{
    public static SubmissionFileDto From(SubmissionFile file, string url) =>
        new(file.Id, file.FileName, url, file.SizeBytes);
}

public record StudentRepoDto(
    string RepoFullName,
    string? LastSha,
    CheckStatus CheckStatus,
    DateTimeOffset UpdatedAt,
    RepoProvisioningState ProvisioningState,
    string? ProvisioningError,
    string? SubmissionSha,
    DateTimeOffset? LockedAt,
    string? LockError,
    DateTimeOffset? ReopenedAt,
    string? LastCommitMessage,
    DateTimeOffset? LastCommitAt,
    int CommitCount,
    int ChecksPassed,
    int ChecksTotal,
    string? InitialSha,
    DateTimeOffset? InitialCommitAt,
    DateTimeOffset? ProvisionedAt,
    DateTimeOffset? CheckStatusChangedAt,
    List<GitHubCheckRunDto> CheckRuns)
{
    public static StudentRepoDto From(StudentRepo repo) =>
        new(
            repo.RepoFullName,
            repo.LastSha,
            repo.CheckStatus,
            repo.UpdatedAt,
            repo.ProvisioningState,
            repo.ProvisioningError,
            repo.SubmissionSha,
            repo.LockedAt,
            repo.LockError,
            repo.ReopenedAt,
            repo.LastCommitMessage,
            repo.LastCommitAt,
            repo.CommitCount,
            repo.ChecksPassed,
            repo.ChecksTotal,
            repo.InitialSha,
            repo.InitialCommitAt,
            repo.ProvisionedAt,
            repo.CheckStatusChangedAt,
            repo.CheckRuns
                .OrderBy(checkRun => checkRun.Name)
                .Select(checkRun => new GitHubCheckRunDto(
                    checkRun.ExternalKey,
                    checkRun.Name,
                    checkRun.Status,
                    checkRun.Conclusion))
                .ToList());
}

public record GitHubCheckRunDto(
    string Id,
    string Name,
    string Status,
    string? Conclusion);

public record GradeDto(Guid GradedById, GradeLevel? Level, string Feedback, DateTimeOffset GradedAt)
{
    public static GradeDto From(Grade grade) => new(grade.GradedById, grade.Level, grade.Feedback, grade.GradedAt);
}
