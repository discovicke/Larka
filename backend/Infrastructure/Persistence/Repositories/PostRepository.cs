using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class PostRepository : IPostRepository
{
    private readonly AppDb _db;

    public PostRepository(AppDb db) => _db = db;

    public Task<List<Post>> GetByGroupAsync(Guid groupId, PostType? type, CancellationToken ct)
    {
        var q = _db.Posts.Where(p => p.GroupId == groupId);
        if (type is { } t)
        {
            q = q.Where(p => p.Type == t);
        }

        return q.OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.PublishAt).ToListAsync(ct);
    }

    public void Add(Post post) => _db.Posts.Add(post);

    public void AddAssignmentConfig(AssignmentConfig config) => _db.AssignmentConfigs.Add(config);

    public Task<List<Comment>> GetCommentsAsync(Guid postId, CancellationToken ct) =>
        _db.Comments.Where(c => c.PostId == postId).OrderBy(c => c.CreatedAt).ToListAsync(ct);

    public void AddComment(Comment comment) => _db.Comments.Add(comment);

    public void AddAttachment(Attachment attachment) => _db.Attachments.Add(attachment);
}
