using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Content skapas fristående och publiceras sedan med en eller flera länkar (AddLinkAsync).
public class ContentService
{
    private readonly IContentRepository _content;
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _time;

    public ContentService(
        IContentRepository content,
        IGroupRepository groups,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        TimeProvider time)
    {
        _content = content;
        _groups = groups;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _time = time;
    }

    public Task<Content?> GetAsync(Guid id, CancellationToken ct) => _content.GetAsync(id, ct);

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

        if (dto.MaxScore is not null && type != ContentType.Assignment)
        {
            return ServiceResult<Content>.Invalid("maxScore is only allowed on assignments");
        }

        var content = new Content
        {
            AuthorId = _currentUser.UserId,
            Type = type,
            Title = dto.Title.Trim(),
            BodyMd = dto.BodyMd,
            Status = status
        };
        if (type == ContentType.Assignment)
        {
            content.Assignment = new AssignmentConfig { ContentId = content.Id, MaxScore = dto.MaxScore ?? 100 };
        }

        _content.Add(content);
        await _unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<Content>.Ok(content);
    }

    public async Task<ServiceResult<ContentLink>> AddLinkAsync(
        Guid contentId, CreateContentLinkDto dto, CancellationToken ct)
    {
        var content = await _content.GetAsync(contentId, ct);
        if (content is null)
        {
            return ServiceResult<ContentLink>.NotFound("content not found");
        }

        if (dto.GroupId is null && dto.CourseId is null)
        {
            return ServiceResult<ContentLink>.Invalid("groupId or courseId is required");
        }

        if (dto.GroupId is { } groupId && !await _groups.ExistsAsync(groupId, ct))
        {
            return ServiceResult<ContentLink>.Invalid("group does not exist");
        }

        if (dto.CourseId is { } courseId && !await _groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<ContentLink>.Invalid("course does not exist");
        }

        if (dto is { GroupId: { } g, CourseId: { } c } && !await _groups.ReadsCourseAsync(g, c, ct))
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
            PublishAt = dto.PublishAt ?? _time.GetUtcNow(),
            DueAt = dto.DueAt,
            IsPinned = dto.IsPinned
        };
        _content.AddLink(link);
        await _unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentLink>.Ok(link);
    }

    /// <summary>Filtrerar på typ (news|theory|assignment) om den går att tolka, annars returneras alla.</summary>
    public Task<List<ContentFeedItem>> GetGroupFeedAsync(Guid groupId, string? type, CancellationToken ct) =>
        _content.GetGroupFeedAsync(groupId, ParseType(type), _time.GetUtcNow(), ct);

    public Task<List<ContentFeedItem>> GetCourseFeedAsync(Guid courseId, string? type, CancellationToken ct) =>
        _content.GetCourseFeedAsync(courseId, ParseType(type), _time.GetUtcNow(), ct);

    public Task<List<Comment>> GetCommentsAsync(Guid contentId, CancellationToken ct) =>
        _content.GetCommentsAsync(contentId, ct);

    public async Task<ServiceResult<Comment>> AddCommentAsync(Guid contentId, CommentDto dto, CancellationToken ct)
    {
        if (!await _content.ExistsAsync(contentId, ct))
        {
            return ServiceResult<Comment>.NotFound("content not found");
        }

        var comment = new Comment { ContentId = contentId, AuthorId = _currentUser.UserId, BodyMd = dto.BodyMd };
        _content.AddComment(comment);
        await _unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<Comment>.Ok(comment);
    }

    public async Task<ServiceResult<Attachment>> AddAttachmentAsync(
        Guid contentId, AttachmentDto dto, CancellationToken ct)
    {
        if (!await _content.ExistsAsync(contentId, ct))
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
        _content.AddAttachment(attachment);
        await _unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<Attachment>.Ok(attachment);
    }

    private static ContentType? ParseType(string? type) =>
        Enum.TryParse<ContentType>(type, true, out var t) ? t : null;
}
