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
    StudentSupportService supportStatus,
    IFileStorage storage,
    NotificationService notifications,
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
    /// kursen. I repo-läge skapas en pending repo-rad som bakgrundsjobbet provisionerar. Sparar inte, det gör
    /// den som anropar.
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
            var now = time.GetUtcNow();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (submission, student) in newSubmissions.Zip(students))
            {
                submission.Repo = new StudentRepo
                {
                    SubmissionId = submission.Id,
                    TemplateRepo = spec.TemplateRepo,
                    AssignmentTitle = content.Title,
                    RepoNamePattern = spec.RepoNamePattern,
                    DesiredRepoName = GitHubRepoNaming.ForStudent(
                        spec.RepoNamePattern, content.Title, student, usedNames),
                    ProvisioningState = RepoProvisioningState.Pending,
                    NextProvisioningAttemptAt = now,
                    UpdatedAt = now
                };
            }
        }

        submissions.AddRange(newSubmissions);
    }

    // Eleven själv, lärarna på kursen och elevens mentor får läsa en inlämning, och därmed dess filer och betyg.
    // Admin som inte är lärare på länken får 403, andra 404.
    public async Task<ServiceResult<SubmissionDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var submission = await submissions.GetAsync(id, ct);
        if (submission is null)
        {
            return ServiceResult<SubmissionDto>.NotFound("submission not found");
        }

        if (submission.StudentId != currentUser.UserId
            && !await MayReadAllAsync(submission.ContentLinkId, ct)
            && !await permissions.IsMentorOfStudentAsync(submission.StudentId, ct))
        {
            return await permissions.IsAdminAsync(ct)
                ? ServiceResult<SubmissionDto>.Forbidden()
                : ServiceResult<SubmissionDto>.NotFound("submission not found");
        }

        return ServiceResult<SubmissionDto>.Ok(await ToDtoAsync(submission, ct));
    }

    // Lärarna ser alla inlämningar på utdelningen. Mentorn ser dem som tillhör elever i mentorns klass, och
    // eleven bara sin egen. Admin som inte är lärare på länken får 403: inlämningar och betyg är lärarens.
    public async Task<ServiceResult<List<SubmissionDto>>> GetByLinkAsync(Guid linkId, CancellationToken ct)
    {
        var linkSubmissions = await submissions.GetByLinkAsync(linkId, ct);
        if (!await MayReadAllAsync(linkId, ct))
        {
            if (await permissions.IsAdminAsync(ct))
            {
                return ServiceResult<List<SubmissionDto>>.Forbidden();
            }

            var mentored = await groups.GetMentoredStudentIdsAsync(currentUser.UserId, ct);
            linkSubmissions = linkSubmissions
                .Where(submission => submission.StudentId == currentUser.UserId
                    || mentored.Contains(submission.StudentId))
                .ToList();
        }

        var names = await users.GetNamesAsync(linkSubmissions.Select(submission => submission.StudentId).ToList(), ct);
        var support = await supportStatus.BuildAsync(linkSubmissions, ct);
        var dtos = new List<SubmissionDto>();
        foreach (var submission in linkSubmissions)
        {
            dtos.Add(await ToDtoAsync(
                submission,
                names.GetValueOrDefault(submission.StudentId, ""),
                support.GetValueOrDefault(submission.Id),
                ct));
        }

        return ServiceResult<List<SubmissionDto>>.Ok(dtos);
    }

    // Att bedöma kräver att man är lärare på länken. Mentorn och admin får bara läsa.
    private async Task<bool> MayReadAllAsync(Guid linkId, CancellationToken ct) =>
        await permissions.CanTeachLinkAsync(linkId, ct);

    private async Task<SubmissionDto> ToDtoAsync(Submission submission, CancellationToken ct)
    {
        var name = (await users.GetAsync(submission.StudentId, ct))?.Name ?? "";
        var support = await supportStatus.BuildAsync([submission], ct);
        return await ToDtoAsync(submission, name, support.GetValueOrDefault(submission.Id), ct);
    }

    // Filernas länkar skapas här, när inlämningen läses, så de går inte att dela vidare och gälla för alltid.
    private async Task<SubmissionDto> ToDtoAsync(
        Submission submission,
        string studentName,
        StudentSupportDto? support,
        CancellationToken ct)
    {
        var files = new List<SubmissionFileDto>();
        foreach (var file in submission.Files)
        {
            var url = await storage.GetSignedUrlAsync(file.StorageKey, IFileStorage.SignedUrlLifetime, ct);
            files.Add(SubmissionFileDto.From(file, url));
        }

        return SubmissionDto.From(submission, studentName, files, support);
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
        var modes = spec?.SubmissionModes ?? [];
        var files = uploads ?? [];
        var inputError = CheckSubmitInput(spec, files);
        if (inputError is not null)
        {
            return ServiceResult<SubmissionDto>.Invalid(inputError);
        }

        var submission = await submissions.GetForStudentAsync(linkId, currentUser.UserId, ct);
        var keptFiles = submission is null || dto.KeepFileIds is null
            ? []
            : submission.Files.Where(file => dto.KeepFileIds.Contains(file.Id)).ToList();
        if (!HasAnswer(modes, dto, files, keptFiles))
        {
            return ServiceResult<SubmissionDto>.Invalid("this assignment needs a text answer or files");
        }

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
        if (UsesRepo(modes) && submission.Repo is not null)
        {
            submission.Repo.SubmissionSha = submission.Repo.LastSha;
        }

        await ReplaceFilesAsync(submission, keptFiles, files, ct);
        return ServiceResult<SubmissionDto>.Ok(await ToDtoAsync(submission, ct));
    }

    private static string? CheckSubmitInput(AssignmentSpec? spec, IReadOnlyList<FileUpload> files)
    {
        if (spec is null || !StudentSubmits(spec.SubmissionModes))
        {
            return "this content takes no submissions";
        }

        if (files.Count > 0 && !spec.SubmissionModes.Contains(SubmissionMode.File))
        {
            return "this assignment does not take files";
        }

        return FileUploadPolicy.Validate(files);
    }

    // Med repo är repot själva inlämningen. Annars måste det finnas något av det uppgiften tar emot.
    private static bool HasAnswer(
        List<SubmissionMode> modes, SubmitDto dto, IReadOnlyList<FileUpload> files, List<SubmissionFile> keptFiles)
    {
        var hasText = modes.Contains(SubmissionMode.Text) && !string.IsNullOrWhiteSpace(dto.BodyMd);
        var hasFiles = modes.Contains(SubmissionMode.File) && files.Count > 0;
        return UsesRepo(modes) || hasText || (modes.Contains(SubmissionMode.File) && keptFiles.Count > 0) || hasFiles;
    }

    private async Task ReplaceFilesAsync(
        Submission submission, List<SubmissionFile> keptFiles, IReadOnlyList<FileUpload> files, CancellationToken ct)
    {
        // Nya filer ersätter de gamla, och först när allt är sparat tas de gamla bort ur lagringen.
        var removedFiles = submission.Files.Where(file => !keptFiles.Contains(file)).ToList();
        var oldKeys = removedFiles.Select(file => file.StorageKey).ToList();
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

            submissions.RemoveFiles(removedFiles);
            submission.Files = [.. keptFiles, .. newFiles];
            submissions.AddFiles(newFiles);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteQuietlyAsync(newFiles.Select(file => file.StorageKey), logger);
            throw;
        }

        await storage.DeleteQuietlyAsync(oldKeys, logger);
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

        var gradeError = CheckGrade(submission, spec, dto);
        if (gradeError is not null)
        {
            return ServiceResult<SubmissionDto>.Invalid(gradeError);
        }

        ApplyGrade(submission, dto);

        // En begärd komplettering ska öppna ett arkiverat repo igen, men får inte fälla betygsättningen.
        if (dto.NeedsWork && submission.Repo is not null)
        {
            var now = time.GetUtcNow();
            submission.Repo.ReopenedAt = now;
            submission.Repo.UpdatedAt = now;
            var student = await users.GetAsync(submission.StudentId, ct);
            if (student is not null && !string.IsNullOrWhiteSpace(submission.Repo.RepoFullName))
            {
                try
                {
                    await provisioner.ReopenForResubmissionAsync(
                        new StudentRepoContext(submission.Repo, student.SchoolId), ct);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    submission.Repo.LockError =
                        "Repot kunde inte öppnas för komplettering. Öppna det manuellt i GitHub.";
                    logger.LogWarning(
                        exception,
                        "Could not reopen GitHub repo for submission {SubmissionId}",
                        submission.Id);
                }
            }
        }

        await notifications.NotifySubmissionAsync(
            submission,
            linked.Content.Id,
            dto.NeedsWork
                ? NotificationKind.SubmissionNeedsWork
                : NotificationKind.SubmissionGraded,
            ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<SubmissionDto>.Ok(await ToDtoAsync(submission, ct));
    }

    private static string? CheckGrade(Submission submission, AssignmentSpec spec, GradeSubmissionDto dto)
    {
        // Vid bedömning på plats finns inget att vänta på: läraren betygsätter i klassrummet.
        var signedOffOnSite = spec.SubmissionModes.Contains(SubmissionMode.OnSite);
        if (submission.Status == SubmissionStatus.NotStarted && !signedOffOnSite)
        {
            return "nothing has been submitted yet";
        }

        if (dto.NeedsWork && dto.Level is not null)
        {
            return "a submission that needs work gets feedback, not a grade";
        }

        return dto.NeedsWork
            ? null
            : CheckLevel(spec.GradeScale, dto.Level);
    }

    private void ApplyGrade(Submission submission, GradeSubmissionDto dto)
    {
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

    /// <summary>Uppdaterar ett repo via GitHub-namn. Används av webhookar.</summary>
    public async Task<bool> RefreshRepoByFullNameAsync(
        string repoFullName, CancellationToken ct)
    {
        var context = await submissions.GetRepoContextByFullNameAsync(repoFullName, ct);
        if (context is null)
        {
            return false;
        }

        await provisioner.RefreshCheckStatusAsync([context], ct);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Uppdaterar endast repon för en utdelning. Läraren ser resultatet direkt i tabellen.</summary>
    public async Task<ServiceResult<bool>> RefreshReposForLinkAsync(Guid linkId, CancellationToken ct)
    {
        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        var now = time.GetUtcNow();
        var contexts = await submissions.GetRepoContextsForLinkAsync(linkId, now, ct);
        var repoContexts = contexts
            .Where(context => context.Repo.ProvisioningState == RepoProvisioningState.Ready)
            .Select(context => new StudentRepoContext(context.Repo, context.SchoolId))
            .ToList();
        if (repoContexts.Count > 0)
        {
            await provisioner.RefreshCheckStatusAsync(repoContexts, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>Låser alla färdiga repon i en utdelning direkt, utan att vänta på deadlinejobbet.</summary>
    public async Task<ServiceResult<int>> LockReposForLinkAsync(Guid linkId, CancellationToken ct)
    {
        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<int>.Forbidden();
        }

        var now = time.GetUtcNow();
        var contexts = await submissions.GetRepoContextsForLinkAsync(linkId, now, ct);
        var locked = 0;
        foreach (var context in contexts)
        {
            if (context.Repo.ProvisioningState != RepoProvisioningState.Ready)
            {
                continue;
            }

            try
            {
                var result = await provisioner.LockAtDeadlineAsync(
                    new StudentRepoContext(context.Repo, context.SchoolId), now, ct);
                if (!result.Locked)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(result.Sha))
                {
                    context.Submission.Status = SubmissionStatus.Submitted;
                    context.Submission.SubmittedAt = result.CommitAt ?? now;
                }

                context.Repo.LockError = result.Error;
                context.Repo.UpdatedAt = now;
                locked++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                context.Repo.LockError = exception is InvalidOperationException
                    ? exception.Message
                    : "Kunde inte låsa GitHub-repot. Försök igen.";
                context.Repo.UpdatedAt = now;
                logger.LogWarning(
                    exception,
                    "Could not lock GitHub repo for submission {SubmissionId}",
                    context.Repo.SubmissionId);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<int>.Ok(locked);
    }
}
