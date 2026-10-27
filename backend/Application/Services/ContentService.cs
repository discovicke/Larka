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
    IGroupRepository groups,
    IModuleRepository modules,
    SubmissionService submissions,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<ContentService> logger)
{
    public async Task<ContentDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var content = await contents.GetAsync(id, ct);
        return content is null
            ? null
            : ContentDto.From(content);
    }

    // Typ, status och läge är enums i DTO:n, så okända värden stoppas redan när JSON läses in (400).
    public async Task<ValidationResult<ContentDto>> CreateAsync(CreateContentDto dto, CancellationToken ct)
    {
        var type = dto.Type;
        var status = dto.Status ?? ContentStatus.Published;

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ValidationResult<ContentDto>.Invalid("title is required");
        }

        var fieldError = CheckAssignmentFields(type, dto.GradeScale, dto.SubmissionModes, dto.TemplateRepo);
        if (fieldError is not null)
        {
            return ValidationResult<ContentDto>.Invalid(fieldError);
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
            content.Assignment = new AssignmentSpec
            {
                ContentId = content.Id,
                GradeScale = scale,
                SubmissionModes = modes,
                TemplateRepo = dto.TemplateRepo
            };
        }

        contents.Add(content);
        await unitOfWork.SaveChangesAsync(ct);
        return ValidationResult<ContentDto>.Ok(ContentDto.From(content));
    }

    public async Task<ServiceResult<ContentLinkDto>> AddLinkAsync(
        Guid contentId, CreateContentLinkDto dto, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null)
        {
            return ServiceResult<ContentLinkDto>.NotFound("content not found");
        }

        if (dto.GroupId is null && dto.CourseId is null)
        {
            return ServiceResult<ContentLinkDto>.Invalid("groupId or courseId is required");
        }

        if (dto.GroupId is not null && !await groups.ExistsAsync(dto.GroupId.Value, ct))
        {
            return ServiceResult<ContentLinkDto>.Invalid("group does not exist");
        }

        if (dto.CourseId is not null && !await groups.CourseExistsAsync(dto.CourseId.Value, ct))
        {
            return ServiceResult<ContentLinkDto>.Invalid("course does not exist");
        }

        var linksGroupAndCourse = dto.GroupId is not null && dto.CourseId is not null;
        if (linksGroupAndCourse && !await groups.ReadsCourseAsync(dto.GroupId!.Value, dto.CourseId!.Value, ct))
        {
            return ServiceResult<ContentLinkDto>.Invalid("group does not read this course");
        }

        var moduleError = await CheckModuleAsync(dto.ModuleId, dto.CourseId, ct);
        if (moduleError is not null)
        {
            return ServiceResult<ContentLinkDto>.Invalid(moduleError);
        }

        if (dto.DueAt is not null && content.Type != ContentType.Assignment)
        {
            return ServiceResult<ContentLinkDto>.Invalid("dueAt is only allowed on assignments");
        }

        var position = dto.ModuleId is null
            ? 0
            : await contents.GetNextLinkPositionAsync(dto.ModuleId.Value, ct);

        var link = new ContentLink
        {
            ContentId = contentId,
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
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentLinkDto>.Ok(ContentLinkDto.From(link));
    }

    /// <summary>Returnerar null om innehållet inte finns.</summary>
    public async Task<ContentDto?> UpdateStatusAsync(Guid id, UpdateContentStatusDto dto, CancellationToken ct)
    {
        var content = await contents.GetAsync(id, ct);
        if (content is null)
        {
            return null;
        }

        content.Status = dto.Status;
        await unitOfWork.SaveChangesAsync(ct);
        return ContentDto.From(content);
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

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ServiceResult<ContentDto>.Invalid("title is required");
        }

        var fieldError = CheckAssignmentFields(
            content.Type, dto.GradeScale, dto.SubmissionModes, dto.TemplateRepo);
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

        if (dto.DueAt is not null && linked.Content.Type != ContentType.Assignment)
        {
            return ServiceResult<ContentLinkDto>.Invalid("dueAt is only allowed on assignments");
        }

        linked.Link.PublishAt = dto.PublishAt ?? time.GetUtcNow();
        linked.Link.DueAt = dto.DueAt;
        linked.Link.IsPinned = dto.IsPinned;
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

        if (IsPublishedGradedAssignment(linked.Content))
        {
            return ServiceResult<bool>.Conflict("unpublish the assignment before deleting it");
        }

        var linkSubmissions = await submissionRepository.GetByLinkAsync(linkId, ct);
        if (linkSubmissions.Any(HasStudentActivity))
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

    /// <summary>Filtrerar på typ (news|theory|assignment) om den går att tolka, annars returneras alla.</summary>
    public Task<List<ContentFeedItem>> GetGroupFeedAsync(Guid groupId, string? type, CancellationToken ct) =>
        contents.GetGroupFeedAsync(groupId, ParseType(type), time.GetUtcNow(), ct);

    public Task<List<ContentFeedItem>> GetCourseFeedAsync(Guid courseId, string? type, CancellationToken ct) =>
        contents.GetCourseFeedAsync(courseId, ParseType(type), time.GetUtcNow(), ct);

    public async Task<List<CommentDto>> GetCommentsAsync(Guid contentId, CancellationToken ct)
    {
        var comments = await contents.GetCommentsAsync(contentId, ct);
        return comments.Select(CommentDto.From).ToList();
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
        return CommentDto.From(comment);
    }

    /// <summary>Sparar filen i lagringen under content/{contentId}. NotFound om innehållet inte finns.</summary>
    public async Task<ServiceResult<AttachmentDto>> AddAttachmentAsync(
        Guid contentId, FileUpload file, CancellationToken ct)
    {
        if (!await contents.ExistsAsync(contentId, ct))
        {
            return ServiceResult<AttachmentDto>.NotFound("content not found");
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
        if (!await contents.ExistsAsync(contentId, ct))
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

    /// <summary>Returnerar false om bilagan inte finns.</summary>
    public async Task<bool> DeleteAttachmentAsync(Guid contentId, Guid attachmentId, CancellationToken ct)
    {
        var attachment = await contents.GetAttachmentAsync(contentId, attachmentId, ct);
        if (attachment is null)
        {
            return false;
        }

        contents.RemoveAttachment(attachment);
        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteAsync(attachment.StorageKey, ct);
        return true;
    }

    private static string? CheckAssignmentFields(
        ContentType type, GradeScale? gradeScale, List<SubmissionMode>? modes, string? templateRepo)
    {
        var hasAssignmentFields = gradeScale is not null || modes is not null || templateRepo is not null;
        if (type != ContentType.Assignment && hasAssignmentFields)
        {
            return "gradeScale, submissionModes and templateRepo are only allowed on assignments";
        }

        var submissionModes = modes ?? [];
        if (SubmissionService.UsesRepo(submissionModes) != (templateRepo is not null))
        {
            return "templateRepo is required for repo, and only allowed there";
        }

        return templateRepo is not null && !LooksLikeOrgAndName(templateRepo)
            ? "templateRepo must look like org/name"
            : null;
    }

    // En publicerad inlämning (uppgift med betygsskala) får inte raderas innan den avpublicerats.
    private static bool IsPublishedGradedAssignment(Content content)
    {
        if (content.Type != ContentType.Assignment || content.Status != ContentStatus.Published)
        {
            return false;
        }

        return content.Assignment is not null && content.Assignment.GradeScale != GradeScale.None;
    }

    // NotStarted-rader skapas när uppgiften delas ut och är inget elevarbete. Repo, filer och betyg är det.
    private static bool HasStudentActivity(Submission submission) =>
        submission.Status != SubmissionStatus.NotStarted
        || submission.SubmittedAt is not null
        || submission.Files.Count > 0
        || submission.Repo is not null
        || submission.Grade is not null;

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
