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
    GitHubRepoActions repoActions,
    StudentSupportService supportStatus,
    IFileStorage storage,
    NotificationService notifications,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<SubmissionService> logger)
{
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
        var groupByStudent = link.Groups
            .SelectMany(group => group.Members.Select(member => (member.StudentId, Group: group)))
            .ToDictionary(entry => entry.StudentId, entry => entry.Group);
        var newSubmissions = students
            .Select(student => new Submission
            {
                ContentLinkId = link.Id,
                StudentId = student.Id,
                AssignmentGroupId = groupByStudent.TryGetValue(student.Id, out var group)
                    ? group.Id
                    : null
            })
            .ToList();

        if (spec.UsesRepo)
        {
            var now = time.GetUtcNow();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (spec.WorkMode == RepoWorkMode.Group)
            {
                foreach (var group in link.Groups.OrderBy(group => group.Position))
                {
                    // Repot hänger på gruppens första inlämning. Alla medlemmar ser samma repo via gruppen.
                    var leader = newSubmissions.FirstOrDefault(submission =>
                        submission.AssignmentGroupId == group.Id);
                    if (leader is null)
                    {
                        continue;
                    }

                    var repo = new StudentRepo
                    {
                        SubmissionId = leader.Id,
                        AssignmentGroupId = group.Id,
                        TemplateRepo = spec.TemplateRepo,
                        AssignmentTitle = content.Title,
                        RepoNamePattern = spec.RepoNamePattern,
                        DesiredRepoName = GitHubRepoNaming.ForGroup(
                            spec.RepoNamePattern, content.Title, group.Name, group.Position, usedNames),
                        ProvisioningState = RepoProvisioningState.Pending,
                        NextProvisioningAttemptAt = now,
                        UpdatedAt = now
                    };
                    leader.Repo = repo;
                    group.Repo = repo;
                }
            }
            else
            {
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
        }

        submissions.AddRange(newSubmissions);
    }

    // Eleven själv, och lärarna på kursen får läsa en inlämning, och därmed dess filer och betyg.
    // Admin som inte är lärare på länken får 403, andra 404.
    public async Task<ServiceResult<SubmissionDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var submission = await submissions.GetAsync(id, ct);
        if (submission is null)
        {
            return ServiceResult<SubmissionDto>.NotFound("submission not found");
        }

        if (submission.StudentId != currentUser.UserId
            && !await MayReadAllAsync(submission.ContentLinkId, ct))
        {
            return await permissions.IsAdminAsync(ct)
                ? ServiceResult<SubmissionDto>.Forbidden()
                : ServiceResult<SubmissionDto>.NotFound("submission not found");
        }

        return ServiceResult<SubmissionDto>.Ok(await ToDtoAsync(submission, ct));
    }

    // Lärarna ser alla inlämningar på utdelningen. Eleven ser bara sin egen. Admin som inte är lärare på länken får 403: inlämningar och betyg är lärarens.
    public async Task<ServiceResult<List<SubmissionDto>>> GetByLinkAsync(Guid linkId, CancellationToken ct)
    {
        var linkSubmissions = await submissions.GetByLinkAsync(linkId, ct);
        if (!await MayReadAllAsync(linkId, ct))
        {
            if (await permissions.IsAdminAsync(ct))
            {
                return ServiceResult<List<SubmissionDto>>.Forbidden();
            }

            linkSubmissions = linkSubmissions
                .Where(submission => submission.StudentId == currentUser.UserId)
                .ToList();
        }

        var studentIds = linkSubmissions.Select(submission => submission.StudentId).ToList();
        var names = await users.GetNamesAsync(studentIds, ct);
        var avatars = await users.GetAvatarUrlsAsync(studentIds, ct);
        var support = await supportStatus.BuildAsync(linkSubmissions, ct);
        var urls = await DownloadUrlsAsync(linkSubmissions, ct);
        var dtos = linkSubmissions
            .Select(submission => ToDto(
                submission,
                names.GetValueOrDefault(submission.StudentId, ""),
                support.GetValueOrDefault(submission.Id),
                urls,
                avatars.GetValueOrDefault(submission.StudentId)))
            .ToList();

        return ServiceResult<List<SubmissionDto>>.Ok(dtos);
    }

    // Att bedöma kräver att man är lärare på länken. Admin får bara läsa.
    private async Task<bool> MayReadAllAsync(Guid linkId, CancellationToken ct) =>
        await permissions.CanTeachLinkAsync(linkId, ct);

    private async Task<SubmissionDto> ToDtoAsync(Submission submission, CancellationToken ct)
    {
        var student = await users.GetAsync(submission.StudentId, ct);
        var support = await supportStatus.BuildAsync([submission], ct);
        return ToDto(
            submission, student?.Name ?? "", support.GetValueOrDefault(submission.Id),
            await DownloadUrlsAsync([submission], ct),
            AvatarPolicy.UrlFor(submission.StudentId, student?.AvatarKey));
    }

    // Filernas länkar skapas här, när inlämningen läses, så de går inte att dela vidare och gälla för alltid. Alla
    // inlämningarnas filer signeras i ett anrop, och de laddas ner under sina riktiga namn.
    private Task<IReadOnlyDictionary<string, string>> DownloadUrlsAsync(
        IEnumerable<Submission> list, CancellationToken ct) =>
        storage.GetDownloadUrlsAsync(
            list.SelectMany(submission => submission.Files)
                .Select(file => new StoredFile(file.StorageKey, file.FileName))
                .ToList(),
            IFileStorage.SignedUrlLifetime,
            ct);

    private static SubmissionDto ToDto(
        Submission submission,
        string studentName,
        StudentSupportDto? support,
        IReadOnlyDictionary<string, string> urls,
        string? studentAvatarUrl)
    {
        var files = submission.Files.Select(file => SubmissionFileDto.From(file, urls[file.StorageKey])).ToList();
        return SubmissionDto.From(submission, studentName, files, support, studentAvatarUrl);
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

        // Bara elever som uppgiften delats ut till får lämna in, och bara när den är publicerad. Lärare, admin och
        // elever i andra grupper får 403 innan något sparas.
        if (!await contents.IsVisibleToMemberOfLinkAsync(linkId, currentUser.UserId, time.GetUtcNow(), ct))
        {
            return ServiceResult<SubmissionDto>.Forbidden();
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
        if (submission?.Status == SubmissionStatus.Graded)
        {
            return ServiceResult<SubmissionDto>.Invalid("already graded");
        }

        var window = GradingWindow.Of(linked.Link, submission, time.GetUtcNow());
        var windowError = window.HandInError(submission?.Status ?? SubmissionStatus.NotStarted);
        if (windowError is not null)
        {
            return ServiceResult<SubmissionDto>.Invalid(windowError);
        }

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

        var submittedAt = time.GetUtcNow();
        // En gruppinlämning är gemensam: när en medlem lämnar in räknas hela gruppen som inlämnad.
        // Individuella betyg rörs inte, eftersom gruppen inte har något gemensamt betyg.
        var groupSubmissions = submission.AssignmentGroupId is not null
            ? await submissions.GetSubmissionsForGroupAsync(submission.AssignmentGroupId.Value, ct)
            : [];
        if (submission.AssignmentGroupId is not null && !groupSubmissions.Contains(submission))
        {
            groupSubmissions.Add(submission);
        }

        foreach (var memberSubmission in groupSubmissions.Count > 0
                     ? groupSubmissions
                     : [submission])
        {
            if (memberSubmission.Status == SubmissionStatus.Graded)
            {
                continue;
            }

            memberSubmission.Status = SubmissionStatus.Submitted;
            memberSubmission.SubmittedAt = submittedAt;
        }

        submission.BodyMd = dto.BodyMd;
        var repo = submission.Repo ?? submission.Group?.Repo;
        if (StudentWorkPolicy.UsesRepo(modes) && repo is not null)
        {
            repo.SubmissionSha = repo.LastSha;
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
        var hasKeptFiles = modes.Contains(SubmissionMode.File) && keptFiles.Count > 0;
        return StudentWorkPolicy.UsesRepo(modes) || hasText || hasFiles || hasKeptFiles;
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

    /// <summary>
    /// Läraren ger en elev en extra deadline, eller tar bort den (DueAt null). Eleven behöver inte ha någon
    /// inlämning än: en NotStarted-rad skapas då. Eleven får en notis när en deadline sätts.
    /// </summary>
    public async Task<ServiceResult<SubmissionDto>> SetPersonalDueAtAsync(
        Guid linkId, Guid studentId, SetPersonalDueAtDto dto, CancellationToken ct)
    {
        var linked = await contents.GetLinkWithContentAsync(linkId, ct);
        if (linked is null)
        {
            return ServiceResult<SubmissionDto>.NotFound("link not found");
        }

        if (!await MayReadAllAsync(linkId, ct))
        {
            return ServiceResult<SubmissionDto>.Forbidden();
        }

        var submission = await submissions.GetForStudentAsync(linkId, studentId, ct);
        var now = time.GetUtcNow();
        var error = CheckPersonalDueAt(linked, submission, dto.DueAt, now);
        if (error is not null)
        {
            return ServiceResult<SubmissionDto>.Invalid(error);
        }

        if (submission is null)
        {
            if (dto.DueAt is null)
            {
                return ServiceResult<SubmissionDto>.NotFound("submission not found");
            }

            if (!await IsInAudienceAsync(linked.Link, studentId, ct))
            {
                return ServiceResult<SubmissionDto>.NotFound("student not found");
            }

            submission = new Submission { ContentLinkId = linkId, StudentId = studentId };
            submissions.Add(submission);
        }

        submission.PersonalDueAt = dto.DueAt;
        if (dto.DueAt is not null)
        {
            await notifications.NotifyPersonalDeadlineSetAsync(submission, linked.Content.Id, ct);
            await ReopenLockedRepoAsync(submission, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<SubmissionDto>.Ok(await ToDtoAsync(submission, ct));
    }

    // Ett låst repo öppnas igen när eleven får en personlig deadline och ännu inte lämnat in (NotStarted)
    // eller ska komplettera (NeedsWork). Samma åtgärd som när läraren begär komplettering. Tas datumet bort
    // låses inget direkt, det gör deadlinejobbet vid nästa körning.
    private async Task ReopenLockedRepoAsync(Submission submission, CancellationToken ct)
    {
        var repo = submission.Repo ?? submission.Group?.Repo;
        var locked = repo is not null && repo.LockedAt is not null && repo.ReopenedAt is null;
        if (!locked || submission.Status is not (SubmissionStatus.NotStarted or SubmissionStatus.NeedsWork))
        {
            return;
        }

        var student = await users.GetAsync(submission.StudentId, ct);
        if (student is not null)
        {
            await repoActions.ReopenAsync(repo!, student.SchoolId, ct);
        }
    }

    private static string? CheckPersonalDueAt(
        LinkedContent linked, Submission? submission, DateTimeOffset? dueAt, DateTimeOffset now)
    {
        if (linked.Content.Assignment is null || linked.Content.Assignment.SubmissionModes.Count == 0)
        {
            return "Bara en inlämningsuppgift kan få en personlig deadline.";
        }

        if (submission?.Status == SubmissionStatus.Graded)
        {
            return "Inlämningen är redan bedömd, så eleven kan inte få en ny deadline.";
        }

        if (dueAt is null)
        {
            return null;
        }

        if (linked.Link.DueAt is null)
        {
            return "Uppgiften har ingen deadline, så eleven kan inte få en egen.";
        }

        // En inlämning som väntar på bedömning är låst. Ska eleven lämna in igen begär läraren komplettering.
        return submission?.Status == SubmissionStatus.Submitted
            ? "Eleven har lämnat in och väntar på bedömning. Begär komplettering om eleven ska få lämna in igen."
            : CheckNewPersonalDueAt(linked, dueAt.Value, now);
    }

    // Ett nytt datum måste ligga i framtiden och efter klassens sista deadline. Före den gör det ingen skillnad,
    // eftersom eleven redan får lämna in till dess.
    private static string? CheckNewPersonalDueAt(LinkedContent linked, DateTimeOffset dueAt, DateTimeOffset now)
    {
        if (linked.Link.DueAt is null)
        {
            return "Uppgiften har ingen deadline, så eleven kan inte få en egen.";
        }

        if (dueAt.UtcDateTime <= now.UtcDateTime)
        {
            return "Den nya deadlinen måste ligga i framtiden.";
        }

        var lastClassDeadline = linked.Link.ResubmitDueAt.Append(linked.Link.DueAt.Value).Max();
        return dueAt.UtcDateTime <= lastClassDeadline.UtcDateTime
            ? "Den egna deadlinen måste ligga efter uppgiftens sista deadline."
            : null;
    }

    // Eleven går i en grupp som uppgiften delats ut till, på samma sätt som när den delas ut.
    private async Task<bool> IsInAudienceAsync(ContentLink link, Guid studentId, CancellationToken ct)
    {
        var groupIds = link.GroupId is not null
            ? [link.GroupId.Value]
            : await groups.GetGroupIdsReadingCourseAsync(link.CourseId!.Value, ct);
        var students = await users.GetStudentsInGroupsAsync(groupIds, ct);
        return students.Any(student => student.Id == studentId);
    }

    /// <summary>Sätter betyg, eller begär komplettering (NeedsWork), som bara tar feedback.</summary>
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
        var now = time.GetUtcNow();

        // Komplettera med en ny deadline: datumet sparas först, så att fönstret nedan räknar med det.
        if (dto.PersonalDueAt is not null)
        {
            var dueError = dto.Outcome == GradeOutcome.NeedsWork
                ? CheckNewPersonalDueAt(linked, dto.PersonalDueAt.Value, now)
                : "En ny deadline hör bara till Komplettera.";
            if (dueError is not null)
            {
                return ServiceResult<SubmissionDto>.Invalid(dueError);
            }

            submission.PersonalDueAt = dto.PersonalDueAt;
        }

        var window = GradingWindow.Of(linked.Link, submission, now);
        var gradeError = CheckGrade(submission, spec, dto, window);
        if (gradeError is not null)
        {
            return ServiceResult<SubmissionDto>.Invalid(gradeError);
        }

        ApplyGrade(submission, dto);

        // En begärd komplettering ska öppna ett arkiverat repo igen, men får inte fälla betygsättningen.
        var repo = submission.Repo ?? submission.Group?.Repo;
        if (dto.Outcome == GradeOutcome.NeedsWork && repo is not null)
        {
            var student = await users.GetAsync(submission.StudentId, ct);
            if (student is not null)
            {
                await repoActions.ReopenAsync(repo, student.SchoolId, ct);
            }
        }

        await notifications.NotifySubmissionAsync(
            submission,
            linked.Content.Id,
            dto.Outcome == GradeOutcome.NeedsWork
                ? NotificationKind.SubmissionNeedsWork
                : NotificationKind.SubmissionGraded,
            ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<SubmissionDto>.Ok(await ToDtoAsync(submission, ct));
    }

    private static string? CheckGrade(
        Submission submission, AssignmentSpec spec, GradeSubmissionDto dto, GradingWindow window)
    {
        var outcome = dto.Outcome;

        // Vid bedömning på plats finns inget att vänta på: läraren betygsätter i klassrummet.
        var signedOffOnSite = spec.SubmissionModes.Contains(SubmissionMode.OnSite);
        if (submission.Status == SubmissionStatus.NotStarted && !signedOffOnSite)
        {
            return "nothing has been submitted yet";
        }

        if (outcome == GradeOutcome.NeedsWork && dto.Level is not null)
        {
            return "a submission that needs work gets feedback, not a grade";
        }

        if (outcome == GradeOutcome.NeedsWork)
        {
            return window.AllowsNeedsWork
                ? null
                : GradingWindow.NeedsWorkTooLateMessage;
        }

        if (dto.Level == GradeLevel.Fail && !window.AllowsFail)
        {
            return GradingWindow.FailTooEarlyMessage;
        }

        // Ett IG måste motiveras så att eleven vet vad som saknas.
        if (dto.Level == GradeLevel.Fail && string.IsNullOrWhiteSpace(dto.Feedback))
        {
            return "a fail grade requires a comment";
        }

        return CheckLevel(spec.GradeScale, dto.Level);
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
        submission.Status = dto.Outcome switch
        {
            GradeOutcome.NeedsWork => SubmissionStatus.NeedsWork,
            _ => SubmissionStatus.Graded
        };
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
            var alreadyLocked = context.Repo.LockedAt is not null && context.Repo.ReopenedAt is null;
            if (context.Repo.ProvisioningState != RepoProvisioningState.Ready || alreadyLocked)
            {
                continue;
            }

            try
            {
                // Läraren låser nu, så nu räknas som deadline. Samma regler som deadlinejobbet gäller:
                // bara repon med egen commit låses så länge en deadline återstår.
                var result = await provisioner.LockAtDeadlineAsync(
                    new StudentRepoContext(context.Repo, context.SchoolId),
                    now,
                    RepoLockPolicy.LocksWithoutOwnCommit(context, now),
                    ct);
                if (!result.Locked)
                {
                    continue;
                }

                if (result.OwnCommit && !string.IsNullOrWhiteSpace(result.Sha))
                {
                    var groupSubmissions = context.Repo.AssignmentGroupId is not null
                        ? await submissions.GetSubmissionsForGroupAsync(
                            context.Repo.AssignmentGroupId.Value, ct)
                        : [context.Submission];
                    RepoLockPolicy.MarkSubmitted(groupSubmissions, result.CommitAt ?? now);
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
