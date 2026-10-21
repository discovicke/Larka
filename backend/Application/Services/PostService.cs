using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

public class PostService
{
    private readonly IPostRepository _posts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public PostService(IPostRepository posts, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _posts = posts;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>Filtrerar på typ (news|theory|assignment) om den går att tolka, annars returneras alla.</summary>
    public Task<List<Post>> GetByGroupAsync(Guid groupId, string? type, CancellationToken ct)
    {
        PostType? filter = Enum.TryParse<PostType>(type, true, out var t) ? t : null;
        return _posts.GetByGroupAsync(groupId, filter, ct);
    }

    /// <summary>Returnerar null om typen inte är news|theory|assignment.</summary>
    public async Task<Post?> CreateAsync(Guid groupId, CreatePostDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<PostType>(dto.Type, true, out var type))
        {
            return null;
        }

        var post = new Post
        {
            GroupId = groupId,
            AuthorId = _currentUser.UserId,
            Type = type,
            Title = dto.Title,
            BodyMd = dto.BodyMd,
            IsPinned = dto.IsPinned
        };
        _posts.Add(post);
        if (type == PostType.Assignment)
        {
            _posts.AddAssignmentConfig(new AssignmentConfig
            {
                PostId = post.Id,
                DueAt = dto.DueAt,
                MaxScore = dto.MaxScore ?? 100
            });
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return post;
    }

    public Task<List<Comment>> GetCommentsAsync(Guid postId, CancellationToken ct) =>
        _posts.GetCommentsAsync(postId, ct);

    public async Task<Comment> AddCommentAsync(Guid postId, CommentDto dto, CancellationToken ct)
    {
        var comment = new Comment { PostId = postId, AuthorId = _currentUser.UserId, BodyMd = dto.BodyMd };
        _posts.AddComment(comment);
        await _unitOfWork.SaveChangesAsync(ct);
        return comment;
    }

    public async Task<Attachment> AddAttachmentAsync(Guid postId, AttachmentDto dto, CancellationToken ct)
    {
        var attachment = new Attachment
        {
            PostId = postId,
            FileName = dto.FileName,
            Url = dto.Url,
            SizeBytes = dto.SizeBytes
        };
        _posts.AddAttachment(attachment);
        await _unitOfWork.SaveChangesAsync(ct);
        return attachment;
    }
}
