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
    public Task<Content?> GetAsync(Guid id, CancellationToken ct) => contents.GetAsync(id, ct);

    public async Task<ServiceResult<Content>> CreateAsync(CreateContentDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<ContentType>(dto.Type, true, out var type))
        {
            return ServiceResult<Content>.Invalid("type must be news|theory|assignment");
        }

        var status = ContentStatus.Published;
        if (dto.Status is not null && !Enum.TryParse(dto.Status, true, out status))
        {
            return ServiceResult<Content>.Invalid("status must be draft|published");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ServiceResult<Content>.Invalid("title is required");
        }

        if (type != ContentType.Assignment && (dto.MaxScore is not null || dto.SubmissionMode is not null || dto.TemplateRepo is not null))
        {
            return ServiceResult<Content>.Invalid("maxScore, submissionMode and templateRepo are only allowed on assignments");
        }

        var mode = SubmissionMode.None;
        if (dto.SubmissionMode is not null && !Enum.TryParse(dto.SubmissionMode, true, out mode))
        {
            return ServiceResult<Content>.Invalid("submissionMode must be none|file|repo|fileOrRepo");
        }

        if (SubmissionService.UsesRepo(mode) != (dto.TemplateRepo is not null))
        {
            return ServiceResult<Content>.Invalid("templateRepo is required for repo and fileOrRepo, and only allowed there");
        }

        if (dto.TemplateRepo is not null && dto.TemplateRepo.Split('/') is not [{ Length: > 0 }, { Length: > 0 }])
        {
            return ServiceResult<Content>.Invalid("templateRepo must look like org/name");
        }

        if (dto.MaxScore is < 1)
        {
            return ServiceResult<Content>.Invalid("maxScore must be at least 1");
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
        return ServiceResult<Content>.Ok(content);
    }

    public async Task<ServiceResult<ContentLink>> AddLinkAsync(
        Guid contentId, CreateContentLinkDto dto, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null)
        {
            return ServiceResult<ContentLink>.NotFound("content not found");
        }

        if (dto.GroupId is null && dto.CourseId is null)
        {
            return ServiceResult<ContentLink>.Invalid("groupId or courseId is required");
        }

        if (dto.GroupId is { } groupId && !await groups.ExistsAsync(groupId, ct))
        {
            return ServiceResult<ContentLink>.Invalid("group does not exist");
        }

        if (dto.CourseId is { } courseId && !await groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<ContentLink>.Invalid("course does not exist");
        }

        if (dto is { GroupId: { } g, CourseId: { } c } && !await groups.ReadsCourseAsync(g, c, ct))
        {
            return ServiceResult<ContentLink>.Invalid("group does not read this course");
        }

        if (dto.DueAt is not null && content.Type != ContentType.Assignment)
        {
            return ServiceResult<ContentLink>.Invalid("dueAt is only allowed on assignments");
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
        return ServiceResult<ContentLink>.Ok(link);
    }

    /// <summary>Filtrerar på typ (news|theory|assignment) om den går att tolka, annars returneras alla.</summary>
    public Task<List<ContentFeedItem>> GetGroupFeedAsync(Guid groupId, string? type, CancellationToken ct) =>
        contents.GetGroupFeedAsync(groupId, ParseType(type), time.GetUtcNow(), ct);

    public Task<List<ContentFeedItem>> GetCourseFeedAsync(Guid courseId, string? type, CancellationToken ct) =>
        contents.GetCourseFeedAsync(courseId, ParseType(type), time.GetUtcNow(), ct);

    public Task<List<Comment>> GetCommentsAsync(Guid contentId, CancellationToken ct) =>
        contents.GetCommentsAsync(contentId, ct);

    public async Task<ServiceResult<Comment>> AddCommentAsync(Guid contentId, CommentDto dto, CancellationToken ct)
    {
        if (!await contents.ExistsAsync(contentId, ct))
        {
            return ServiceResult<Comment>.NotFound("content not found");
        }

        var comment = new Comment { ContentId = contentId, AuthorId = currentUser.UserId, BodyMd = dto.BodyMd };
        contents.AddComment(comment);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<Comment>.Ok(comment);
    }

    public async Task<ServiceResult<Attachment>> AddAttachmentAsync(
        Guid contentId, AttachmentDto dto, CancellationToken ct)
    {
        if (!await contents.ExistsAsync(contentId, ct))
        {
            return ServiceResult<Attachment>.NotFound("content not found");
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
        return ServiceResult<Attachment>.Ok(attachment);
    }

    private static ContentType? ParseType(string? type) =>
        Enum.TryParse<ContentType>(type, true, out var t) ? t : null;
}
