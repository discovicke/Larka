using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Inlämningar hör till en utdelning (ContentLink) av en uppgift. Hur man lämnar in styrs av
// AssignmentSpec.SubmissionMode: filer, GitHub-repo eller båda.
public class SubmissionService
{
    private readonly ISubmissionRepository _submissions;
    private readonly IContentRepository _content;
    private readonly IGroupRepository _groups;
    private readonly IUserRepository _users;
    private readonly ILabProvisioner _provisioner;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public SubmissionService(
        ISubmissionRepository submissions,
        IContentRepository content,
        IGroupRepository groups,
        IUserRepository users,
        ILabProvisioner provisioner,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        TimeProvider time)
    {
        _submissions = submissions;
        _content = content;
        _groups = groups;
        _users = users;
        _provisioner = provisioner;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _time = time;
    }

    public static bool UsesRepo(SubmissionMode mode) => mode is SubmissionMode.Repo or SubmissionMode.FileOrRepo;

    /// <summary>
    /// Delar ut en uppgift: en inlämning (NotStarted) per elev i länkens grupp, eller i alla grupper som läser
    /// kursen. I repo-läge får varje elev ett repo från mallen. Sparar inte, det gör den som anropar.
    /// </summary>
    public async Task HandOutAsync(ContentLink link, Content content, CancellationToken ct)
    {
        if (content.Assignment is not { SubmissionMode: not SubmissionMode.None } spec)
        {
            return;
        }

        var groupIds = link.GroupId is { } groupId
            ? [groupId]
            : await _groups.GetGroupIdsReadingCourseAsync(link.CourseId!.Value, ct);
        var students = await _users.GetStudentsInGroupsAsync(groupIds, ct);
        var submissions = students
            .Select(s => new Submission { ContentLinkId = link.Id, StudentId = s.Id })
            .ToList();

        if (UsesRepo(spec.SubmissionMode))
        {
            var requests = submissions.Zip(students, (sub, student) => new RepoRequest(sub.Id, student)).ToList();
            var repos = await _provisioner.ProvisionAsync(spec.TemplateRepo!, content.Title, requests, ct);
            foreach (var repo in repos)
            {
                submissions.Single(s => s.Id == repo.SubmissionId).Repo = repo;
            }
        }

        _submissions.AddRange(submissions);
    }

    public Task<Submission?> GetAsync(Guid id, CancellationToken ct) => _submissions.GetAsync(id, ct);

    public Task<List<Submission>> GetByLinkAsync(Guid linkId, CancellationToken ct) =>
        _submissions.GetByLinkAsync(linkId, ct);

    /// <summary>Lämnar in (eller lämnar in igen) som inloggad elev. En inlämning per elev och utdelning.</summary>
    public async Task<ServiceResult<Submission>> SubmitAsync(Guid linkId, SubmitDto dto, CancellationToken ct)
    {
        var linked = await _content.GetLinkWithContentAsync(linkId, ct);
        if (linked is null)
        {
            return ServiceResult<Submission>.NotFound("link not found");
        }

        if (linked.Content.Assignment is not { SubmissionMode: not SubmissionMode.None } spec)
        {
            return ServiceResult<Submission>.Invalid("this content takes no submissions");
        }

        var files = dto.Files ?? [];
        if (spec.SubmissionMode == SubmissionMode.File && files.Count == 0)
        {
            return ServiceResult<Submission>.Invalid("this assignment needs at least one file");
        }

        if (spec.SubmissionMode == SubmissionMode.Repo && files.Count > 0)
        {
            return ServiceResult<Submission>.Invalid("this assignment is handed in through its repo, not files");
        }

        var submission = await _submissions.GetForStudentAsync(linkId, _currentUser.UserId, ct);
        if (submission is null)
        {
            // Eleven fanns inte i gruppen när uppgiften delades ut.
            submission = new Submission { ContentLinkId = linkId, StudentId = _currentUser.UserId };
            _submissions.Add(submission);
        }
        else if (submission.Status == SubmissionStatus.Graded)
        {
            return ServiceResult<Submission>.Invalid("already graded");
        }

        submission.Status = SubmissionStatus.Submitted;
        submission.SubmittedAt = _time.GetUtcNow();
        submission.BodyMd = dto.BodyMd;
        _submissions.RemoveFiles(submission.Files);
        submission.Files = files.Select(f => new SubmissionFile
        {
            SubmissionId = submission.Id,
            FileName = f.FileName,
            Url = f.Url,
            SizeBytes = f.SizeBytes
        }).ToList();
        _submissions.AddFiles(submission.Files);

        await _unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<Submission>.Ok(submission);
    }

    /// <summary>Sätter betyg, eller begär komplettering (NeedsWork) med bara feedback.</summary>
    public async Task<ServiceResult<Submission>> GradeAsync(Guid submissionId, GradeDto dto, CancellationToken ct)
    {
        var submission = await _submissions.GetAsync(submissionId, ct);
        if (submission is null)
        {
            return ServiceResult<Submission>.NotFound("submission not found");
        }

        if (submission.Status == SubmissionStatus.NotStarted)
        {
            return ServiceResult<Submission>.Invalid("nothing has been submitted yet");
        }

        var linked = await _content.GetLinkWithContentAsync(submission.ContentLinkId, ct);
        var maxScore = linked!.Content.Assignment?.MaxScore;
        if (dto.NeedsWork && dto.Score is not null)
        {
            return ServiceResult<Submission>.Invalid("a submission that needs work gets feedback, not a score");
        }

        if (!dto.NeedsWork && maxScore is null && dto.Score is not null)
        {
            return ServiceResult<Submission>.Invalid("this assignment has no score, only feedback");
        }

        if (!dto.NeedsWork && maxScore is { } max && (dto.Score is null || dto.Score < 0 || dto.Score > max))
        {
            return ServiceResult<Submission>.Invalid($"score must be between 0 and {max}");
        }

        if (submission.Grade is null)
        {
            submission.Grade = new Grade { SubmissionId = submission.Id };
            _submissions.AddGrade(submission.Grade);
        }

        submission.Grade.GradedById = _currentUser.UserId;
        submission.Grade.Score = dto.Score;
        submission.Grade.Feedback = dto.Feedback;
        submission.Grade.GradedAt = _time.GetUtcNow();
        submission.Status = dto.NeedsWork ? SubmissionStatus.NeedsWork : SubmissionStatus.Graded;

        await _unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<Submission>.Ok(submission);
    }

    public async Task RefreshChecksAsync(CancellationToken ct)
    {
        var repos = await _submissions.GetAllReposAsync(ct);
        await _provisioner.RefreshCheckStatusAsync(repos, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
