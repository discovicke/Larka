using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Abstractions;

public interface IPostRepository
{
    Task<List<Post>> GetByGroupAsync(Guid groupId, PostType? type, CancellationToken ct);
    void Add(Post post);
    void AddAssignmentConfig(AssignmentConfig config);
    Task<List<Comment>> GetCommentsAsync(Guid postId, CancellationToken ct);
    void AddComment(Comment comment);
    void AddAttachment(Attachment attachment);
}
