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
    IGroupRepository groups,
    IModuleRepository modules,
    SubmissionService submissions,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time)
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
        var modes = (dto.SubmissionModes ?? []).Distinct().ToList();
        var scale = dto.GradeScale ?? GradeScale.None;

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ValidationResult<ContentDto>.Invalid("title is required");
        }

        var hasAssignmentFields = dto.GradeScale is not null || dto.SubmissionModes is not null
            || dto.TemplateRepo is not null;
        if (type != ContentType.Assignment && hasAssignmentFields)
        {
            return ValidationResult<ContentDto>.Invalid(
                "gradeScale, submissionModes and templateRepo are only allowed on assignments");
        }

        if (SubmissionService.UsesRepo(modes) != (dto.TemplateRepo is not null))
        {
            return ValidationResult<ContentDto>.Invalid("templateRepo is required for repo, and only allowed there");
        }

        if (dto.TemplateRepo is not null && !LooksLikeOrgAndName(dto.TemplateRepo))
        {
            return ValidationResult<ContentDto>.Invalid("templateRepo must look like org/name");
        }

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

        var link = new ContentLink
        {
            ContentId = contentId,
            GroupId = dto.GroupId,
            CourseId = dto.CourseId,
            ModuleId = dto.ModuleId,
            PublishAt = dto.PublishAt ?? time.GetUtcNow(),
            DueAt = dto.DueAt,
            IsPinned = dto.IsPinned
        };
        contents.AddLink(link);
        await submissions.HandOutAsync(link, content, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentLinkDto>.Ok(ContentLinkDto.From(link));
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
