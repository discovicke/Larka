using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

// --- requests ---

// Filerna kommer med uppladdningen (FileUpload). Utan KeepFileIds ersätter de tidigare filer vid ny inlämning
// (t.ex. efter komplettering). Med KeepFileIds behålls de filerna och resten tas bort, så att eleven kan
// ändra texten eller byta en enda fil utan att skicka om alla.
public record SubmitDto(string? BodyMd = null, List<Guid>? KeepFileIds = null);

// Utfallet av lärarens bedömning. Graded sätter betyg, NeedsWork begär komplettering och NotSubmitted markerar
// att eleven inte lämnat in. De två sista tar bara feedback, inget betyg.
public enum GradeOutcome
{
    Graded,
    NeedsWork,
    NotSubmitted
}

public record GradeSubmissionDto(string Feedback, GradeLevel? Level = null, GradeOutcome Outcome = GradeOutcome.Graded);

// --- responses ---

/// <summary>Hur många elever som markerades som Ej inlämnat i en och samma åtgärd.</summary>
public record MarkedNotSubmittedDto(int Count);

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
    StudentSupportDto? Support,
    AssignmentGroupDto? Group = null)
{
    public static SubmissionDto From(
        Submission submission,
        string studentName,
        List<SubmissionFileDto> files,
        StudentSupportDto? support = null)
    {
        var repo = submission.Repo ?? submission.Group?.Repo;
        var grade = submission.Grade is null
            ? null
            : GradeDto.From(submission.Grade);
        var group = submission.Group is null
            ? null
            : AssignmentGroupDto.From(submission.Group);
        return new SubmissionDto(
            submission.Id, submission.ContentLinkId, submission.StudentId, studentName, submission.Status,
            submission.BodyMd, submission.SubmittedAt, files,
            repo is null
                ? null
                : StudentRepoDto.From(repo),
            grade, support, group);
    }
}

public record AssignmentGroupDto(Guid Id, string Name, List<AssignmentGroupMemberDto> Members)
{
    public static AssignmentGroupDto From(AssignmentGroup group) =>
        new(
            group.Id,
            group.Name,
            group.Members
                .Select(member => new AssignmentGroupMemberDto(
                    member.StudentId,
                    member.User?.Name ?? ""))
                .OrderBy(member => member.Name)
                .ToList());
}

public record AssignmentGroupMemberDto(Guid Id, string Name);

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
