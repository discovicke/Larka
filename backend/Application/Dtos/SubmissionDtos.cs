using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

// --- requests ---

// Används även i svaret, se SubmissionDto.Files.
public record SubmissionFileDto(string FileName, string Url, long SizeBytes)
{
    public static SubmissionFileDto From(SubmissionFile file) => new(file.FileName, file.Url, file.SizeBytes);
}

// Files ersätter tidigare filer vid ny inlämning (t.ex. efter komplettering).
public record SubmitDto(string? BodyMd = null, List<SubmissionFileDto>? Files = null);

// NeedsWork = begär komplettering: bara feedback, ingen poäng.
public record GradeSubmissionDto(string Feedback, int? Score = null, bool NeedsWork = false);

// --- responses ---

public record SubmissionDto(
    Guid Id,
    Guid ContentLinkId,
    Guid StudentId,
    SubmissionStatus Status,
    string? BodyMd,
    DateTimeOffset? SubmittedAt,
    List<SubmissionFileDto> Files,
    StudentRepoDto? Repo,
    GradeDto? Grade)
{
    public static SubmissionDto From(Submission submission)
    {
        var repo = submission.Repo is null
            ? null
            : StudentRepoDto.From(submission.Repo);
        var grade = submission.Grade is null
            ? null
            : GradeDto.From(submission.Grade);
        return new SubmissionDto(
            submission.Id, submission.ContentLinkId, submission.StudentId, submission.Status, submission.BodyMd,
            submission.SubmittedAt, submission.Files.Select(SubmissionFileDto.From).ToList(), repo, grade);
    }
}

public record StudentRepoDto(string RepoFullName, string? LastSha, CheckStatus CheckStatus, DateTimeOffset UpdatedAt)
{
    public static StudentRepoDto From(StudentRepo repo) =>
        new(repo.RepoFullName, repo.LastSha, repo.CheckStatus, repo.UpdatedAt);
}

public record GradeDto(Guid GradedById, int? Score, string Feedback, DateTimeOffset GradedAt)
{
    public static GradeDto From(Grade grade) => new(grade.GradedById, grade.Score, grade.Feedback, grade.GradedAt);
}
