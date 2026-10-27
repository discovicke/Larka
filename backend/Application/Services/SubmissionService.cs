using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Files;
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
    IPermissions permissions,
    ILabProvisioner provisioner,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<SubmissionService> logger)
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

    // Bara eleven själv och lärarna på kursen får läsa en inlämning, och därmed dess filer. Övriga får null (404).
    public async Task<SubmissionDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var submission = await submissions.GetAsync(id, ct);
        if (submission is null || !await MayReadAsync(submission.ContentLinkId, submission.StudentId, ct))
        {
            return null;
        }

        return await ToDtoAsync(submission, ct);
    }

    // Lärare och admin ser alla inlämningar på utdelningen. Eleven ser bara sin egen.
    public async Task<List<SubmissionDto>> GetByLinkAsync(Guid linkId, CancellationToken ct)
    {
        var linkSubmissions = await submissions.GetByLinkAsync(linkId, ct);
        if (!await MayReadAllAsync(linkId, ct))
        {
            linkSubmissions = linkSubmissions.Where(submission => submission.StudentId == currentUser.UserId).ToList();
        }

        var dtos = new List<SubmissionDto>();
        foreach (var submission in linkSubmissions)
        {
            dtos.Add(await ToDtoAsync(submission, ct));
        }

        return dtos;
    }

    private async Task<bool> MayReadAsync(Guid linkId, Guid studentId, CancellationToken ct) =>
        studentId == currentUser.UserId || await MayReadAllAsync(linkId, ct);

    private async Task<bool> MayReadAllAsync(Guid linkId, CancellationToken ct) =>
        await permissions.CanTeachLinkAsync(linkId, ct);

    // Filernas länkar skapas här, när inlämningen läses, så de går inte att dela vidare och gälla för alltid.
    private async Task<SubmissionDto> ToDtoAsync(Submission submission, CancellationToken ct)
    {
        var files = new List<SubmissionFileDto>();
        foreach (var file in submission.Files)
        {
            var url = await storage.GetSignedUrlAsync(file.StorageKey, IFileStorage.SignedUrlLifetime, ct);
            files.Add(SubmissionFileDto.From(file, url));
        }

        return SubmissionDto.From(submission, files);
    }

    /// <summary>
    /// Lämnar in (eller lämnar in igen) som inloggad elev. En inlämning per elev och utdelning.
    /// Filerna ersätter tidigare filer och sparas i lagringen under submissions/{submissionId}.
    /// </summary>
    public async Task<ServiceResult<SubmissionDto>> SubmitAsync(
        Guid linkId, SubmitDto dto, IReadOnlyList<FileUpload>? uploads, CancellationToken ct)
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
        var files = uploads ?? [];
        if (files.Count > 0 && !modes.Contains(SubmissionMode.File))
        {
            return ServiceResult<SubmissionDto>.Invalid("this assignment does not take files");
        }

        var fileError = FileUploadPolicy.Validate(files);
        if (fileError is not null)
        {
            return ServiceResult<SubmissionDto>.Invalid(fileError);
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

        // Nya filer ersätter de gamla, och först när allt är sparat tas de gamla bort ur lagringen.
        var oldKeys = submission.Files.Select(file => file.StorageKey).ToList();
        var newFiles = new List<SubmissionFile>();
        try
        {
            foreach (var upload in files)
            {
                var contentType = FileUploadPolicy.ContentTypeFor(upload.FileName)!;
                var key = await storage.UploadAsync($"submissions/{submission.Id}", upload.Content, contentType, ct);
                newFiles.Add(new SubmissionFile
                {
                    SubmissionId = submission.Id,
                    FileName = FileUploadPolicy.CleanFileName(upload.FileName),
                    StorageKey = key,
                    SizeBytes = upload.SizeBytes
                });
            }

            submissions.RemoveFiles(submission.Files);
            submission.Files = newFiles;
            submissions.AddFiles(newFiles);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await DeleteQuietlyAsync(newFiles.Select(file => file.StorageKey));
            throw;
        }

        await DeleteQuietlyAsync(oldKeys);
        return ServiceResult<SubmissionDto>.Ok(await ToDtoAsync(submission, ct));
    }

    // Allt är redan sparat här, så en fil som blir kvar i lagringen ska inte fälla anropet.
    private async Task DeleteQuietlyAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not delete {StorageKey} from file storage", key);
            }
        }
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

        // Svaret innehåller filernas länkar, så bara lärarna på länken får också bedöma den.
        if (!await MayReadAllAsync(submission.ContentLinkId, ct))
        {
            return ServiceResult<SubmissionDto>.Forbidden();
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
        return ServiceResult<SubmissionDto>.Ok(await ToDtoAsync(submission, ct));
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
