using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Inlämningar hör till en utdelning (ContentLink) av en uppgift. Hur man lämnar in styrs av
// AssignmentSpec.SubmissionMode: filer, GitHub-repo eller båda.
public class SubmissionService(
    ISubmissionRepository submissions,
    IContentRepository contents,
    IGroupRepository groups,
    IUserRepository users,
    ILabProvisioner provisioner,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time)
{
    public static bool UsesRepo(SubmissionMode mode) => mode is SubmissionMode.Repo or SubmissionMode.FileOrRepo;

    /// <summary>
    /// Delar ut en uppgift: en inlämning (NotStarted) per elev i länkens grupp, eller i alla grupper som läser
    /// kursen. I repo-läge får varje elev ett repo från mallen. Sparar inte, det gör den som anropar.
    /// </summary>
    public async Task HandOutAsync(ContentLink link, Content content, CancellationToken ct)
    {
        var spec = content.Assignment;
        if (spec is null || spec.SubmissionMode == SubmissionMode.None)
        {
            return;
        }

        var groupIds = link.GroupId is not null
            ? [link.GroupId.Value]
            : await groups.GetGroupIdsReadingCourseAsync(link.CourseId!.Value, ct);
        var students = await users.GetStudentsInGroupsAsync(groupIds, ct);
        var newSubmissions = students
            .Select(student => new Submission { ContentLinkId = link.Id, StudentId = student.Id })
            .ToList();

        if (UsesRepo(spec.SubmissionMode))
        {
            var requests = newSubmissions
                .Zip(students, (submission, student) => new RepoRequest(submission.Id, student))
                .ToList();
            var repos = await provisioner.ProvisionAsync(spec.TemplateRepo!, content.Title, requests, ct);
            foreach (var repo in repos)
            {
                newSubmissions.Single(submission => submission.Id == repo.SubmissionId).Repo = repo;
            }
        }

        submissions.AddRange(newSubmissions);
    }

    public async Task<SubmissionDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var submission = await submissions.GetAsync(id, ct);
        return submission is null
            ? null
            : SubmissionDto.From(submission);
    }

    public async Task<List<SubmissionDto>> GetByLinkAsync(Guid linkId, CancellationToken ct)
    {
        var linkSubmissions = await submissions.GetByLinkAsync(linkId, ct);
        return linkSubmissions.Select(SubmissionDto.From).ToList();
    }

    /// <summary>Lämnar in (eller lämnar in igen) som inloggad elev. En inlämning per elev och utdelning.</summary>
    public async Task<ServiceResult<SubmissionDto>> SubmitAsync(Guid linkId, SubmitDto dto, CancellationToken ct)
    {
        var linked = await contents.GetLinkWithContentAsync(linkId, ct);
        if (linked is null)
        {
            return ServiceResult<SubmissionDto>.NotFound("link not found");
        }

        var spec = linked.Content.Assignment;
        if (spec is null || spec.SubmissionMode == SubmissionMode.None)
        {
            return ServiceResult<SubmissionDto>.Invalid("this content takes no submissions");
        }

        var files = dto.Files ?? [];
        if (spec.SubmissionMode == SubmissionMode.File && files.Count == 0)
        {
            return ServiceResult<SubmissionDto>.Invalid("this assignment needs at least one file");
        }

        if (spec.SubmissionMode == SubmissionMode.Repo && files.Count > 0)
        {
            return ServiceResult<SubmissionDto>.Invalid("this assignment is handed in through its repo, not files");
        }

        var submission = await submissions.GetForStudentAsync(linkId, currentUser.UserId, ct);
        if (submission is null)
        {
            // Eleven fanns inte i gruppen när uppgiften delades ut.
            submission = new Submission { ContentLinkId = linkId, StudentId = currentUser.UserId };
            submissions.Add(submission);
        }
        else if (submission.Status == SubmissionStatus.Graded)
        {
            return ServiceResult<SubmissionDto>.Invalid("already graded");
        }

        submission.Status = SubmissionStatus.Submitted;
        submission.SubmittedAt = time.GetUtcNow();
        submission.BodyMd = dto.BodyMd;
        submissions.RemoveFiles(submission.Files);
        submission.Files = files.Select(file => new SubmissionFile
        {
            SubmissionId = submission.Id,
            FileName = file.FileName,
            Url = file.Url,
            SizeBytes = file.SizeBytes
        }).ToList();
        submissions.AddFiles(submission.Files);

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<SubmissionDto>.Ok(SubmissionDto.From(submission));
    }

    /// <summary>Sätter betyg, eller begär komplettering (NeedsWork) med bara feedback.</summary>
    public async Task<ServiceResult<SubmissionDto>> GradeAsync(
        Guid submissionId, GradeSubmissionDto dto, CancellationToken ct)
    {
        var submission = await submissions.GetAsync(submissionId, ct);
        if (submission is null)
        {
            return ServiceResult<SubmissionDto>.NotFound("submission not found");
        }

        if (submission.Status == SubmissionStatus.NotStarted)
        {
            return ServiceResult<SubmissionDto>.Invalid("nothing has been submitted yet");
        }

        var linked = await contents.GetLinkWithContentAsync(submission.ContentLinkId, ct);
        var maxScore = linked!.Content.Assignment?.MaxScore;
        if (dto.NeedsWork && dto.Score is not null)
        {
            return ServiceResult<SubmissionDto>.Invalid("a submission that needs work gets feedback, not a score");
        }

        if (!dto.NeedsWork && maxScore is null && dto.Score is not null)
        {
            return ServiceResult<SubmissionDto>.Invalid("this assignment has no score, only feedback");
        }

        var scoreOutOfRange = dto.Score is null || dto.Score < 0 || dto.Score > maxScore;
        if (!dto.NeedsWork && maxScore is not null && scoreOutOfRange)
        {
            return ServiceResult<SubmissionDto>.Invalid($"score must be between 0 and {maxScore}");
        }

        if (submission.Grade is null)
        {
            submission.Grade = new Grade { SubmissionId = submission.Id };
            submissions.AddGrade(submission.Grade);
        }

        submission.Grade.GradedById = currentUser.UserId;
        submission.Grade.Score = dto.Score;
        submission.Grade.Feedback = dto.Feedback;
        submission.Grade.GradedAt = time.GetUtcNow();
        submission.Status = dto.NeedsWork
            ? SubmissionStatus.NeedsWork
            : SubmissionStatus.Graded;

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<SubmissionDto>.Ok(SubmissionDto.From(submission));
    }

    public async Task RefreshChecksAsync(CancellationToken ct)
    {
        var repos = await submissions.GetAllReposAsync(ct);
        await provisioner.RefreshCheckStatusAsync(repos, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
