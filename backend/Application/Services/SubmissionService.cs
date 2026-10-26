using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Inlämningar hör till en utdelning (ContentLink) av en uppgift. Hur man lämnar in styrs av
// AssignmentSpec.SubmissionModes: text, filer, GitHub-repo eller bedömning på plats, ett eller flera av dem.
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
    public static bool UsesRepo(List<SubmissionMode> modes) => modes.Contains(SubmissionMode.Repo);

    // Eleven lämnar själv in text, filer eller repo. OnSite bedöms av läraren och har ingen inlämning från eleven.
    private static bool StudentSubmits(List<SubmissionMode> modes) =>
        modes.Any(mode => mode != SubmissionMode.OnSite);

    /// <summary>
    /// Delar ut en uppgift: en inlämning (NotStarted) per elev i länkens grupp, eller i alla grupper som läser
    /// kursen. I repo-läge får varje elev ett repo från mallen. Sparar inte, det gör den som anropar.
    /// </summary>
    public async Task HandOutAsync(ContentLink link, Content content, CancellationToken ct)
    {
        var spec = content.Assignment;
        if (spec is null || spec.SubmissionModes.Count == 0)
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

        if (UsesRepo(spec.SubmissionModes))
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
        if (spec is null || !StudentSubmits(spec.SubmissionModes))
        {
            return ServiceResult<SubmissionDto>.Invalid("this content takes no submissions");
        }

        var modes = spec.SubmissionModes;
        var files = dto.Files ?? [];
        if (files.Count > 0 && !modes.Contains(SubmissionMode.File))
        {
            return ServiceResult<SubmissionDto>.Invalid("this assignment does not take files");
        }

        // Med repo är repot själva inlämningen. Annars måste det finnas något av det uppgiften tar emot.
        var hasText = modes.Contains(SubmissionMode.Text) && !string.IsNullOrWhiteSpace(dto.BodyMd);
        var hasFiles = modes.Contains(SubmissionMode.File) && files.Count > 0;
        if (!UsesRepo(modes) && !hasText && !hasFiles)
        {
            return ServiceResult<SubmissionDto>.Invalid("this assignment needs a text answer or files");
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

        var linked = await contents.GetLinkWithContentAsync(submission.ContentLinkId, ct);
        var spec = linked!.Content.Assignment!;

        // Vid bedömning på plats finns inget att vänta på: läraren betygsätter i klassrummet.
        var signedOffOnSite = spec.SubmissionModes.Contains(SubmissionMode.OnSite);
        if (submission.Status == SubmissionStatus.NotStarted && !signedOffOnSite)
        {
            return ServiceResult<SubmissionDto>.Invalid("nothing has been submitted yet");
        }

        if (dto.NeedsWork && dto.Level is not null)
        {
            return ServiceResult<SubmissionDto>.Invalid("a submission that needs work gets feedback, not a grade");
        }

        var levelError = dto.NeedsWork
            ? null
            : CheckLevel(spec.GradeScale, dto.Level);
        if (levelError is not null)
        {
            return ServiceResult<SubmissionDto>.Invalid(levelError);
        }

        if (submission.Grade is null)
        {
            submission.Grade = new Grade { SubmissionId = submission.Id };
            submissions.AddGrade(submission.Grade);
        }

        submission.Grade.GradedById = currentUser.UserId;
        submission.Grade.Level = dto.Level;
        submission.Grade.Feedback = dto.Feedback;
        submission.Grade.GradedAt = time.GetUtcNow();
        submission.Status = dto.NeedsWork
            ? SubmissionStatus.NeedsWork
            : SubmissionStatus.Graded;

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<SubmissionDto>.Ok(SubmissionDto.From(submission));
    }

    // Null om betyget passar skalan, annars varför inte.
    private static string? CheckLevel(GradeScale scale, GradeLevel? level) =>
        (scale, level) switch
        {
            (GradeScale.None, null) => null,
            (GradeScale.None, _) => "this assignment has no grade scale, only feedback",
            (_, null) => "level is required on this grade scale",
            (GradeScale.PassFail, GradeLevel.Distinction) => "this grade scale is IG-G, so Distinction is not allowed",
            _ => null
        };

    public async Task RefreshChecksAsync(CancellationToken ct)
    {
        var repos = await submissions.GetAllReposAsync(ct);
        await provisioner.RefreshCheckStatusAsync(repos, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
