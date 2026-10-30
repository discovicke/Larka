using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Content skapas fristående och publiceras sedan med en eller flera länkar (AddLinkAsync).
public class ContentService(
    IContentRepository contents,
    ISubmissionRepository submissionRepository,
    IPartCompletionRepository completions,
    IGroupRepository groups,
    IUserRepository users,
    IModuleRepository modules,
    SubmissionService submissions,
    NotificationService notifications,
    IPermissions permissions,
    ICurrentSchool currentSchool,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<ContentService> logger)
{
    // Ett utkast syns bara för dem som får ändra det, och för admin i skolan som läser det.
    public async Task<ContentDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var content = await contents.GetAsync(id, ct);
        return content is null || !await MayReadAsync(content, ct)
            ? null
            : ContentDto.From(content);
    }

    /// <summary>
    /// Visar läraren samma reponamn som provisioning skapar. Exempelvärden används när eleven inte är vald.
    /// </summary>
    public async Task<ServiceResult<RepoNamePreviewDto>> PreviewRepoNameAsync(
        RepoNamePreviewRequest request, CancellationToken ct)
    {
        if (!await permissions.CanCreateContentAsync(ct))
        {
            return ServiceResult<RepoNamePreviewDto>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(request.AssignmentTitle))
        {
            return ServiceResult<RepoNamePreviewDto>.Invalid("assignmentTitle is required");
        }

        var patternError = GitHubRepoNaming.Validate(request.Pattern);
        if (patternError is not null)
        {
            return ServiceResult<RepoNamePreviewDto>.Invalid(patternError);
        }

        var values = new RepoNameValues(
            request.AssignmentTitle,
            request.GitHubLogin,
            string.IsNullOrWhiteSpace(request.StudentName)
                ? "Elev"
                : request.StudentName,
            string.IsNullOrWhiteSpace(request.StudentEmail)
                ? "elev@example.com"
                : request.StudentEmail,
            request.StudentId ?? Guid.Empty);
        var pattern = GitHubRepoNaming.EffectivePattern(request.Pattern);
        return ServiceResult<RepoNamePreviewDto>.Ok(
            new RepoNamePreviewDto(GitHubRepoNaming.Render(request.Pattern, values), pattern));
    }

    // Typ, status och läge är enums i DTO:n, så okända värden stoppas redan när JSON läses in (400).
    public async Task<ServiceResult<ContentDto>> CreateAsync(CreateContentDto dto, CancellationToken ct)
    {
        if (!await permissions.CanCreateContentAsync(ct))
        {
            return ServiceResult<ContentDto>.Forbidden();
        }

        var built = BuildContent(dto);
        if (built.Error is not null)
        {
            return ServiceResult<ContentDto>.Invalid(built.Error);
        }

        var content = built.Value!;
        contents.Add(content);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentDto>.Ok(ContentDto.From(content));
    }

    // Innehåll och länk skapas tillsammans. Ett enda SaveChanges gör att ett fel i länken inte lämnar
    // ett content utan länk, och att utdelningen av uppgifter hamnar i samma transaktion.
    public async Task<ServiceResult<CreatedContentWithLinkDto>> CreateWithLinkAsync(
        CreateContentWithLinkDto dto, CancellationToken ct)
    {
        if (!await permissions.CanCreateContentAsync(ct))
        {
            return ServiceResult<CreatedContentWithLinkDto>.Forbidden();
        }

        if (dto.Content is null || dto.Link is null)
        {
            return ServiceResult<CreatedContentWithLinkDto>.Invalid("content and link are required");
        }

        var built = BuildContent(dto.Content);
        if (built.Error is not null)
        {
            return ServiceResult<CreatedContentWithLinkDto>.Invalid(built.Error);
        }

        var content = built.Value!;
        var linkError = await CheckLinkAsync(content, dto.Link, ct);
        if (linkError is not null)
        {
            return ServiceResult<CreatedContentWithLinkDto>.Invalid(linkError);
        }

        if (!await MayLinkAsync(dto.Link, ct))
        {
            return ServiceResult<CreatedContentWithLinkDto>.Forbidden();
        }

        contents.Add(content);
        var link = await AddLinkCoreAsync(content, dto.Link, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<CreatedContentWithLinkDto>.Ok(
            new CreatedContentWithLinkDto(ContentDto.From(content), ContentLinkDto.From(link)));
    }

    public async Task<ServiceResult<ContentLinkDto>> AddLinkAsync(
        Guid contentId, CreateContentLinkDto dto, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null)
        {
            return ServiceResult<ContentLinkDto>.NotFound("content not found");
        }

        var linkError = await CheckLinkAsync(content, dto, ct);
        if (linkError is not null)
        {
            return ServiceResult<ContentLinkDto>.Invalid(linkError);
        }

        // En länk till en kurs kräver lärare på kursen, en länk till bara en grupp lärare i gruppen.
        if (!await MayLinkAsync(dto, ct))
        {
            return ServiceResult<ContentLinkDto>.Forbidden();
        }

        var link = await AddLinkCoreAsync(content, dto, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentLinkDto>.Ok(ContentLinkDto.From(link));
    }

    /// <summary>
    /// Flyttar en del till en annan vecka i samma kurs. Länken behåller id och deadline.
    /// Beroenden tas bort eftersom de bara gäller inom en vecka.
    /// </summary>
    public async Task<ServiceResult<MoveContentLinkResultDto>> MoveLinkAsync(
        Guid contentId, Guid linkId, MoveContentLinkDto dto, CancellationToken ct)
    {
        var linked = await contents.GetLinkWithContentAsync(contentId, linkId, ct);
        if (linked is null)
        {
            return ServiceResult<MoveContentLinkResultDto>.NotFound("link not found");
        }

        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<MoveContentLinkResultDto>.Forbidden();
        }

        var link = linked.Link;
        if (link.ModuleId is null || link.CourseId is null)
        {
            return ServiceResult<MoveContentLinkResultDto>.Invalid("link is not in a week");
        }

        var sourceModule = await modules.GetAsync(link.ModuleId.Value, ct);
        var targetModule = await modules.GetAsync(dto.ModuleId, ct);
        if (sourceModule is null || targetModule is null)
        {
            return ServiceResult<MoveContentLinkResultDto>.Invalid("module not found");
        }

        if (targetModule.CourseId != link.CourseId)
        {
            return ServiceResult<MoveContentLinkResultDto>.Invalid("module does not belong to the course");
        }

        if (targetModule.Id == sourceModule.Id)
        {
            return ServiceResult<MoveContentLinkResultDto>.Invalid("link is already in that module");
        }

        var linkSubmissions = await submissionRepository.GetByLinkAsync(link.Id, ct);
        if (linkSubmissions.Any(StudentWorkPolicy.HasStudentActivity)
            || await completions.AnyForLinkAsync(link.Id, ct))
        {
            return ServiceResult<MoveContentLinkResultDto>.Conflict(
                "the part has student activity and cannot be moved");
        }

        var dependencies = await contents.GetDependenciesForCourseAsync(link.CourseId.Value, ct);
        var affected = dependencies
            .Where(dependency => dependency.BlockedLinkId == link.Id || dependency.BlockingLinkId == link.Id)
            .ToList();
        var affectedLinkIds = affected
            .SelectMany(dependency => new[] { dependency.BlockedLinkId, dependency.BlockingLinkId })
            .Where(affectedId => affectedId != link.Id)
            .Distinct()
            .ToList();
        contents.RemoveDependencies(affected);

        var sourceLinks = await contents.GetLinksForModuleAsync(sourceModule.Id, ct);
        var remaining = sourceLinks
            .Where(sourceLink => sourceLink.Id != link.Id)
            .OrderBy(sourceLink => sourceLink.Position)
            .ToList();
        for (var i = 0; i < remaining.Count; i++)
        {
            remaining[i].Position = i + 1;
        }

        link.ModuleId = targetModule.Id;
        link.Position = await contents.GetNextLinkPositionAsync(targetModule.Id, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ServiceResult<MoveContentLinkResultDto>.Ok(
            new MoveContentLinkResultDto(link.Id, targetModule.Id, link.Position, affectedLinkIds));
    }

    /// <summary>NotFound om innehållet inte finns, 403 om användaren inte får ändra det.</summary>
    public async Task<ServiceResult<ContentDto>> UpdateStatusAsync(
        Guid id, UpdateContentStatusDto dto, CancellationToken ct)
    {
        var content = await contents.GetAsync(id, ct);
        if (content is null)
        {
            return ServiceResult<ContentDto>.NotFound("content not found");
        }

        if (!await MayEditAsync(content, ct))
        {
            return ServiceResult<ContentDto>.Forbidden();
        }

        var wasPublished = content.Status == ContentStatus.Published;
        content.Status = dto.Status;
        if (content.Status == ContentStatus.Published && !wasPublished)
        {
            await notifications.NotifyContentPublishedAsync(content, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentDto>.Ok(ContentDto.From(content));
    }

    /// <summary>Uppdaterar innehållet. Typen är låst och status ändras via UpdateStatusAsync.</summary>
    public async Task<ServiceResult<ContentDto>> UpdateAsync(
        Guid id, UpdateContentDto dto, CancellationToken ct)
    {
        var content = await contents.GetAsync(id, ct);
        if (content is null)
        {
            return ServiceResult<ContentDto>.NotFound("content not found");
        }

        if (!await MayEditAsync(content, ct))
        {
            return ServiceResult<ContentDto>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ServiceResult<ContentDto>.Invalid("title is required");
        }

        var fieldError = CheckAssignmentFields(
            content.Type,
            dto.GradeScale,
            dto.SubmissionModes,
            dto.TemplateRepo,
            dto.RepoNamePattern,
            dto.WorkMode ?? RepoWorkMode.Individual,
            dto.FeedbackPullRequest ?? false,
            dto.AllowLate ?? false,
            dto.Requirements);
        if (fieldError is not null)
        {
            return ServiceResult<ContentDto>.Invalid(fieldError);
        }

        content.Title = dto.Title.Trim();
        content.BodyMd = dto.BodyMd;

        if (content.Type == ContentType.Assignment)
        {
            var spec = content.Assignment ?? new AssignmentSpec { ContentId = content.Id };
            spec.GradeScale = dto.GradeScale ?? GradeScale.None;
            spec.SubmissionModes = (dto.SubmissionModes ?? []).Distinct().ToList();
            spec.TemplateRepo = dto.TemplateRepo;
            ApplyGitHubSettings(
                spec,
                dto.RepoNamePattern,
                dto.WorkMode ?? RepoWorkMode.Individual,
                dto.LockAtDeadline ?? true,
                dto.FeedbackPullRequest ?? false,
                dto.AllowLate ?? false);
            ApplyRequirements(spec, dto.Requirements);
            // En regel som inte längre kan uppfyllas får inte låsa nästa del för alltid.
            if (spec.GradeScale == GradeScale.None || spec.SubmissionModes.Count == 0)
            {
                spec.CompletionRule = CompletionRule.Submitted;
            }

            content.Assignment = spec;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentDto>.Ok(ContentDto.From(content));
    }

    /// <summary>Uppdaterar publicering och deadline på en länk. Flytt mellan veckor görs separat.</summary>
    public async Task<ServiceResult<ContentLinkDto>> UpdateLinkAsync(
        Guid contentId, Guid linkId, UpdateContentLinkDto dto, CancellationToken ct)
    {
        var linked = await contents.GetLinkWithContentAsync(contentId, linkId, ct);
        if (linked is null)
        {
            return ServiceResult<ContentLinkDto>.NotFound("link not found");
        }

        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<ContentLinkDto>.Forbidden();
        }

        if (dto.DueAt is not null && linked.Content.Type != ContentType.Assignment)
        {
            return ServiceResult<ContentLinkDto>.Invalid("dueAt is only allowed on assignments");
        }

        var previousPublishAt = linked.Link.PublishAt;
        linked.Link.PublishAt = dto.PublishAt ?? time.GetUtcNow();
        linked.Link.DueAt = dto.DueAt;
        linked.Link.IsPinned = dto.IsPinned;

        // Bara en ändrad publiceringstid ger notiser. Annars skulle en nål på en gammal nyhet notifiera alla.
        if (linked.Link.PublishAt != previousPublishAt)
        {
            await notifications.NotifyLinkPublishedAsync(linked.Content, linked.Link, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentLinkDto>.Ok(ContentLinkDto.From(linked.Link));
    }

    /// <summary>Sätter position 1, 2, 3 … i den ordning läraren skickade.</summary>
    public async Task<ServiceResult<bool>> ReorderModuleContentAsync(
        Guid courseId, Guid moduleId, ReorderContentDto dto, CancellationToken ct)
    {
        if (!await groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<bool>.NotFound("course not found");
        }

        if (!await permissions.CanTeachCourseAsync(courseId, ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        var module = await modules.GetAsync(moduleId, ct);
        if (module is null || module.CourseId != courseId)
        {
            return ServiceResult<bool>.NotFound("module not found");
        }

        if (dto.LinkIds is null)
        {
            return ServiceResult<bool>.Invalid("linkIds is required");
        }

        var links = await contents.GetLinksForModuleAsync(moduleId, ct);
        var byId = links.ToDictionary(link => link.Id);
        var listsEveryLinkOnce = dto.LinkIds.Count == links.Count
            && dto.LinkIds.Distinct().Count() == links.Count
            && dto.LinkIds.All(byId.ContainsKey);
        if (!listsEveryLinkOnce)
        {
            return ServiceResult<bool>.Invalid("linkIds must list every part in the module once");
        }

        for (var i = 0; i < dto.LinkIds.Count; i++)
        {
            byId[dto.LinkIds[i]].Position = i + 1;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// Tar bort länken. En publicerad inlämning och innehåll där eleven redan arbetat skyddas.
    /// Tomma inlämningsrader som skapades vid utdelningen tas bort med länken.
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteLinkAsync(
        Guid contentId, Guid linkId, CancellationToken ct)
    {
        var linked = await contents.GetLinkWithContentAsync(contentId, linkId, ct);
        if (linked is null)
        {
            return ServiceResult<bool>.NotFound("link not found");
        }

        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        if (StudentWorkPolicy.IsPublishedGradedAssignment(linked.Content))
        {
            return ServiceResult<bool>.Conflict("unpublish the assignment before deleting it");
        }

        var linkSubmissions = await submissionRepository.GetByLinkAsync(linkId, ct);
        if (linkSubmissions.Any(StudentWorkPolicy.HasStudentActivity)
            || await completions.AnyForLinkAsync(linkId, ct))
        {
            return ServiceResult<bool>.Conflict(
                "the part has student activity, files or a repository and cannot be deleted");
        }

        var deleteContent = await contents.CountLinksAsync(contentId, ct) <= 1;
        List<Attachment> attachments = deleteContent
            ? await contents.GetAttachmentsAsync(contentId, ct)
            : [];

        if (linked.Link.ModuleId is not null)
        {
            var moduleLinks = await contents.GetLinksForModuleAsync(linked.Link.ModuleId.Value, ct);
            var remaining = moduleLinks
                .Where(link => link.Id != linkId)
                .OrderBy(link => link.Position)
                .ToList();
            for (var i = 0; i < remaining.Count; i++)
            {
                remaining[i].Position = i + 1;
            }
        }

        foreach (var submission in linkSubmissions)
        {
            submissionRepository.Remove(submission);
        }

        contents.RemoveLink(linked.Link);
        if (deleteContent)
        {
            contents.RemoveContent(linked.Content);
        }

        await unitOfWork.SaveChangesAsync(ct);
        foreach (var attachment in attachments)
        {
            await DeleteStorageFileQuietlyAsync(attachment.StorageKey);
        }

        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// Filtrerar på typ (news|theory|assignment) om den går att tolka, annars returneras alla. Bara gruppens
    /// medlemmar, lärare i en kurs gruppen läser och admin i skolan får läsa. Utkast ingår aldrig i flödet.
    /// </summary>
    public async Task<ServiceResult<List<ContentFeedItem>>> GetGroupFeedAsync(
        Guid groupId, string? type, CancellationToken ct)
    {
        if (!await groups.ExistsAsync(groupId, ct))
        {
            return ServiceResult<List<ContentFeedItem>>.NotFound("group not found");
        }

        if (!await permissions.CanReadGroupAsync(groupId, ct))
        {
            return ServiceResult<List<ContentFeedItem>>.Forbidden();
        }

        return ServiceResult<List<ContentFeedItem>>.Ok(
            await contents.GetGroupFeedAsync(groupId, ParseType(type), time.GetUtcNow(), ct));
    }

    /// <summary>
    /// Som gruppens flöde, men för kursen: lärare, mentor och elev i kursen, och admin i skolan får läsa.
    /// </summary>
    public async Task<ServiceResult<List<ContentFeedItem>>> GetCourseFeedAsync(
        Guid courseId, string? type, CancellationToken ct)
    {
        if (!await groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<List<ContentFeedItem>>.NotFound("course not found");
        }

        if (!await permissions.CanReadCourseAsync(courseId, ct))
        {
            return ServiceResult<List<ContentFeedItem>>.Forbidden();
        }

        return ServiceResult<List<ContentFeedItem>>.Ok(
            await contents.GetCourseFeedAsync(courseId, ParseType(type), time.GetUtcNow(), ct));
    }

    public async Task<List<CommentDto>> GetCommentsAsync(Guid contentId, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null || !await MayReadAsync(content, ct))
        {
            return [];
        }

        var comments = await contents.GetCommentsAsync(contentId, ct);
        var names = await users.GetNamesAsync(comments.Select(comment => comment.AuthorId).Distinct().ToList(), ct);
        return comments
            .Select(comment => CommentDto.From(comment, names.GetValueOrDefault(comment.AuthorId, "")))
            .ToList();
    }

    /// <summary>Returnerar null om innehållet inte finns.</summary>
    public async Task<CommentDto?> AddCommentAsync(Guid contentId, CreateCommentDto dto, CancellationToken ct)
    {
        if (!await contents.ExistsAsync(contentId, ct))
        {
            return null;
        }

        var comment = new Comment { ContentId = contentId, AuthorId = currentUser.UserId, BodyMd = dto.BodyMd };
        contents.AddComment(comment);
        await unitOfWork.SaveChangesAsync(ct);
        var author = await users.GetAsync(currentUser.UserId, ct);
        return CommentDto.From(comment, author?.Name ?? "");
    }

    /// <summary>Sparar filen i lagringen under content/{contentId}. NotFound om innehållet inte finns.</summary>
    public async Task<ServiceResult<AttachmentDto>> AddAttachmentAsync(
        Guid contentId, FileUpload file, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null)
        {
            return ServiceResult<AttachmentDto>.NotFound("content not found");
        }

        if (!await MayEditAsync(content, ct))
        {
            return ServiceResult<AttachmentDto>.Forbidden();
        }

        var error = FileUploadPolicy.Validate([file]);
        if (error is not null)
        {
            return ServiceResult<AttachmentDto>.Invalid(error);
        }

        var key = await storage.UploadAsync(
            $"content/{contentId}", file.Content, FileUploadPolicy.ContentTypeFor(file.FileName)!, ct);
        var attachment = new Attachment
        {
            ContentId = contentId,
            FileName = FileUploadPolicy.CleanFileName(file.FileName),
            StorageKey = key,
            SizeBytes = file.SizeBytes
        };
        contents.AddAttachment(attachment);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        return ServiceResult<AttachmentDto>.Ok(await ToDtoAsync(attachment, ct));
    }

    /// <summary>Länkarna skapas här, när bilagorna läses. Returnerar null om innehållet inte finns.</summary>
    public async Task<List<AttachmentDto>?> GetAttachmentsAsync(Guid contentId, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null || !await MayReadAsync(content, ct))
        {
            return null;
        }

        var attachments = await contents.GetAttachmentsAsync(contentId, ct);
        var dtos = new List<AttachmentDto>();
        foreach (var attachment in attachments)
        {
            dtos.Add(await ToDtoAsync(attachment, ct));
        }

        return dtos;
    }

    /// <summary>NotFound om bilagan inte finns, 403 om användaren inte får ändra innehållet.</summary>
    public async Task<ServiceResult<bool>> DeleteAttachmentAsync(
        Guid contentId, Guid attachmentId, CancellationToken ct)
    {
        var attachment = await contents.GetAttachmentAsync(contentId, attachmentId, ct);
        if (attachment is null)
        {
            return ServiceResult<bool>.NotFound("attachment not found");
        }

        var content = await contents.GetAsync(contentId, ct);
        if (content is null)
        {
            return ServiceResult<bool>.NotFound("content not found");
        }

        if (!await MayEditAsync(content, ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        contents.RemoveAttachment(attachment);
        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteAsync(attachment.StorageKey, ct);
        return ServiceResult<bool>.Ok(true);
    }

    private ValidationResult<Content> BuildContent(CreateContentDto dto)
    {
        var type = dto.Type;
        var status = dto.Status ?? ContentStatus.Published;

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ValidationResult<Content>.Invalid("title is required");
        }

        var fieldError = CheckAssignmentFields(
            type,
            dto.GradeScale,
            dto.SubmissionModes,
            dto.TemplateRepo,
            dto.RepoNamePattern,
            dto.WorkMode ?? RepoWorkMode.Individual,
            dto.FeedbackPullRequest ?? false,
            dto.AllowLate ?? false,
            dto.Requirements);
        if (fieldError is not null)
        {
            return ValidationResult<Content>.Invalid(fieldError);
        }

        var modes = (dto.SubmissionModes ?? []).Distinct().ToList();
        var scale = dto.GradeScale ?? GradeScale.None;

        var content = new Content
        {
            AuthorId = currentUser.UserId,
            Type = type,
            Title = dto.Title.Trim(),
            BodyMd = dto.BodyMd,
            Status = status
        };
        if (type == ContentType.Assignment)
        {
            var spec = new AssignmentSpec
            {
                ContentId = content.Id,
                GradeScale = scale,
                SubmissionModes = modes,
                TemplateRepo = dto.TemplateRepo
            };
            ApplyGitHubSettings(
                spec,
                dto.RepoNamePattern,
                dto.WorkMode ?? RepoWorkMode.Individual,
                dto.LockAtDeadline ?? true,
                dto.FeedbackPullRequest ?? false,
                dto.AllowLate ?? false);
            ApplyRequirements(spec, dto.Requirements);
            content.Assignment = spec;
        }

        return ValidationResult<Content>.Ok(content);
    }

    private async Task<string?> CheckLinkAsync(
        Content content, CreateContentLinkDto dto, CancellationToken ct)
    {
        if (dto.GroupId is null && dto.CourseId is null)
        {
            return "groupId or courseId is required";
        }

        if (dto.GroupId is not null && !await groups.ExistsAsync(dto.GroupId.Value, ct))
        {
            return "group does not exist";
        }

        if (dto.CourseId is not null && !await groups.CourseExistsAsync(dto.CourseId.Value, ct))
        {
            return "course does not exist";
        }

        var linksGroupAndCourse = dto.GroupId is not null && dto.CourseId is not null;
        if (linksGroupAndCourse && !await groups.ReadsCourseAsync(dto.GroupId!.Value, dto.CourseId!.Value, ct))
        {
            return "group does not read this course";
        }

        var moduleError = await CheckModuleAsync(dto.ModuleId, dto.CourseId, ct);
        if (moduleError is not null)
        {
            return moduleError;
        }

        return dto.DueAt is not null && content.Type != ContentType.Assignment
            ? "dueAt is only allowed on assignments"
            : null;
    }

    private async Task<ContentLink> AddLinkCoreAsync(
        Content content, CreateContentLinkDto dto, CancellationToken ct)
    {
        var position = dto.ModuleId is null
            ? 0
            : await contents.GetNextLinkPositionAsync(dto.ModuleId.Value, ct);

        var link = new ContentLink
        {
            ContentId = content.Id,
            GroupId = dto.GroupId,
            CourseId = dto.CourseId,
            ModuleId = dto.ModuleId,
            Position = position,
            PublishAt = dto.PublishAt ?? time.GetUtcNow(),
            DueAt = dto.DueAt,
            IsPinned = dto.IsPinned
        };
        contents.AddLink(link);
        await submissions.HandOutAsync(link, content, ct);
        await notifications.NotifyLinkPublishedAsync(content, link, ct);
        return link;
    }

    private static string? CheckAssignmentFields(
        ContentType type,
        GradeScale? gradeScale,
        List<SubmissionMode>? modes,
        string? templateRepo,
        string? repoNamePattern,
        RepoWorkMode workMode,
        bool feedbackPullRequest,
        bool allowLate,
        List<AssignmentRequirementInput>? requirements)
    {
        var hasAssignmentFields = gradeScale is not null
            || modes is not null
            || templateRepo is not null
            || !string.IsNullOrWhiteSpace(repoNamePattern)
            || feedbackPullRequest
            || allowLate
            || workMode != RepoWorkMode.Individual
            || requirements is not null && requirements.Count > 0;
        if (type != ContentType.Assignment && hasAssignmentFields)
        {
            return "gradeScale, submissionModes and GitHub settings are only allowed on assignments";
        }

        var submissionModes = modes ?? [];
        var usesRepo = SubmissionService.UsesRepo(submissionModes);
        if (usesRepo != (templateRepo is not null))
        {
            return "templateRepo is required for repo, and only allowed there";
        }

        if (!usesRepo)
        {
            return !string.IsNullOrWhiteSpace(repoNamePattern)
                || feedbackPullRequest
                || allowLate
                || workMode != RepoWorkMode.Individual
                ? "repoNamePattern and repo work settings are only allowed with repo submission mode"
                : null;
        }

        if (workMode != RepoWorkMode.Individual)
        {
            return "group repositories are not supported yet";
        }

        if (feedbackPullRequest)
        {
            return "feedback pull requests are not supported yet";
        }

        if (allowLate)
        {
            return "late submissions are not supported yet";
        }

        if (!LooksLikeOrgAndName(templateRepo!))
        {
            return "templateRepo must look like org/name";
        }

        if (requirements is not null && requirements.Count > 0)
        {
            foreach (var requirement in requirements)
            {
                if (string.IsNullOrWhiteSpace(requirement.Title))
                {
                    return "every requirement needs a title";
                }

                if (!requirement.IsManual && string.IsNullOrWhiteSpace(requirement.TestPattern))
                {
                    return "requirements that are not manual need a test pattern";
                }
            }
        }

        return GitHubRepoNaming.Validate(repoNamePattern);
    }

    private static void ApplyGitHubSettings(
        AssignmentSpec spec,
        string? repoNamePattern,
        RepoWorkMode workMode,
        bool lockAtDeadline,
        bool feedbackPullRequest,
        bool allowLate)
    {
        if (!SubmissionService.UsesRepo(spec.SubmissionModes))
        {
            spec.RepoNamePattern = null;
            spec.WorkMode = RepoWorkMode.Individual;
            spec.LockAtDeadline = true;
            spec.FeedbackPullRequest = false;
            spec.AllowLate = false;
            return;
        }

        spec.RepoNamePattern = string.IsNullOrWhiteSpace(repoNamePattern)
            ? null
            : repoNamePattern.Trim();
        spec.WorkMode = workMode;
        spec.LockAtDeadline = lockAtDeadline;
        spec.FeedbackPullRequest = feedbackPullRequest;
        spec.AllowLate = allowLate;
    }

    private static void ApplyRequirements(
        AssignmentSpec spec, List<AssignmentRequirementInput>? requirements)
    {
        spec.Requirements.Clear();
        if (!SubmissionService.UsesRepo(spec.SubmissionModes) || requirements is null)
        {
            return;
        }

        for (var index = 0; index < requirements.Count; index++)
        {
            var requirement = requirements[index];
            spec.Requirements.Add(new AssignmentRequirement
            {
                ContentId = spec.ContentId,
                Title = requirement.Title.Trim(),
                TestPattern = requirement.IsManual
                    ? null
                    : requirement.TestPattern?.Trim(),
                IsManual = requirement.IsManual,
                Position = index
            });
        }
    }

    // Lärare på någon av innehållets länkar, eller den som skrev innehållet. Admin har ingen genväg hit.
    private async Task<bool> MayEditAsync(Content content, CancellationToken ct) =>
        content.AuthorId == currentUser.UserId || await permissions.CanTeachContentAsync(content.Id, ct);

    // Publicerat innehåll läses av alla inloggade, ett utkast bara av dem som får ändra det och av admin i skolan.
    private async Task<bool> MayReadAsync(Content content, CancellationToken ct)
    {
        if (content.Status != ContentStatus.Draft || await MayEditAsync(content, ct))
        {
            return true;
        }

        var author = await users.GetAsync(content.AuthorId, ct);
        return author is not null
            && await permissions.IsAdminAsync(ct)
            && await currentSchool.OwnsAsync(author.SchoolId, ct);
    }

    // En länk till en kurs kräver lärare på kursen, en länk till bara en grupp lärare i gruppen.
    // CheckLinkAsync har redan kontrollerat att minst en av dem finns.
    private async Task<bool> MayLinkAsync(CreateContentLinkDto dto, CancellationToken ct) =>
        dto.CourseId is not null
            ? await permissions.CanTeachCourseAsync(dto.CourseId.Value, ct)
            : await permissions.CanTeachGroupAsync(dto.GroupId!.Value, ct);

    private async Task DeleteStorageFileQuietlyAsync(string key)
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

    private async Task<AttachmentDto> ToDtoAsync(Attachment attachment, CancellationToken ct) =>
        AttachmentDto.From(
            attachment,
            await storage.GetSignedUrlAsync(attachment.StorageKey, IFileStorage.SignedUrlLifetime, ct));

    /// <summary>Returnerar felet, eller null om modulen saknas eller hör till kursen.</summary>
    private async Task<string?> CheckModuleAsync(Guid? moduleId, Guid? courseId, CancellationToken ct)
    {
        if (moduleId is null)
        {
            return null;
        }

        if (courseId is null)
        {
            return "moduleId requires courseId";
        }

        var module = await modules.GetAsync(moduleId.Value, ct);
        if (module is null)
        {
            return "module does not exist";
        }

        return module.CourseId == courseId
            ? null
            : "module does not belong to the course";
    }

    private static ContentType? ParseType(string? type) =>
        Enum.TryParse<ContentType>(type, true, out var contentType)
            ? contentType
            : null;

    // "org/name": exakt ett snedstreck, med text på båda sidor.
    private static bool LooksLikeOrgAndName(string repo)
    {
        var parts = repo.Split('/');
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0;
    }
}
