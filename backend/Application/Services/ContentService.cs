using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Content skapas fristående och publiceras sedan med en eller flera länkar (AddLinkAsync).
public class ContentService(
    IContentRepository contents,
    IGroupRepository groups,
    SubmissionService submissions,
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
        var mode = dto.SubmissionMode ?? SubmissionMode.None;

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ValidationResult<ContentDto>.Invalid("title is required");
        }

        var hasAssignmentFields =
            dto.MaxScore is not null || dto.SubmissionMode is not null || dto.TemplateRepo is not null;
        if (type != ContentType.Assignment && hasAssignmentFields)
        {
            return ValidationResult<ContentDto>.Invalid(
                "maxScore, submissionMode and templateRepo are only allowed on assignments");
        }

        if (SubmissionService.UsesRepo(mode) != (dto.TemplateRepo is not null))
        {
            return ValidationResult<ContentDto>.Invalid(
                "templateRepo is required for repo and fileOrRepo, and only allowed there");
        }

        if (dto.TemplateRepo is not null && !LooksLikeOrgAndName(dto.TemplateRepo))
        {
            return ValidationResult<ContentDto>.Invalid("templateRepo must look like org/name");
        }

        if (dto.MaxScore < 1)
        {
            return ValidationResult<ContentDto>.Invalid("maxScore must be at least 1");
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
                MaxScore = dto.MaxScore,
                SubmissionMode = mode,
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

        if (dto.DueAt is not null && content.Type != ContentType.Assignment)
        {
            return ServiceResult<ContentLinkDto>.Invalid("dueAt is only allowed on assignments");
        }

        var link = new ContentLink
        {
            ContentId = contentId,
            GroupId = dto.GroupId,
            CourseId = dto.CourseId,
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

    /// <summary>Returnerar null om innehållet inte finns.</summary>
    public async Task<AttachmentDto?> AddAttachmentAsync(Guid contentId, CreateAttachmentDto dto, CancellationToken ct)
    {
        if (!await contents.ExistsAsync(contentId, ct))
        {
            return null;
        }

        var attachment = new Attachment
        {
            ContentId = contentId,
            FileName = dto.FileName,
            Url = dto.Url,
            SizeBytes = dto.SizeBytes
        };
        contents.AddAttachment(attachment);
        await unitOfWork.SaveChangesAsync(ct);
        return AttachmentDto.From(attachment);
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
