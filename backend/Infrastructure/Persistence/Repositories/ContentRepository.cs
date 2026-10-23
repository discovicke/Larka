using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class ContentRepository(AppDb db) : IContentRepository
{
    public Task<Content?> GetAsync(Guid id, CancellationToken ct) =>
        db.Content.Include(c => c.Assignment).FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => db.Content.AnyAsync(c => c.Id == id, ct);

    public void Add(Content content) => db.Content.Add(content);

    public void AddLink(ContentLink link) => db.ContentLinks.Add(link);

    public async Task<LinkedContent?> GetLinkWithContentAsync(Guid linkId, CancellationToken ct)
    {
        var link = await db.ContentLinks.FirstOrDefaultAsync(l => l.Id == linkId, ct);
        if (link is null)
        {
            return null;
        }

        var content = await db.Content.Include(c => c.Assignment).SingleAsync(c => c.Id == link.ContentId, ct);
        return new LinkedContent(link, content);
    }

    public Task<List<ContentFeedItem>> GetGroupFeedAsync(
        Guid groupId, ContentType? type, DateTimeOffset now, CancellationToken ct)
    {
        var courseIds = db.GroupCourses.Where(gc => gc.GroupId == groupId).Select(gc => gc.CourseId);
        var links = db.ContentLinks.Where(l =>
            l.GroupId == groupId || (l.GroupId == null && l.CourseId != null && courseIds.Contains(l.CourseId.Value)));
        return FeedAsync(links, type, now, ct);
    }

    public Task<List<ContentFeedItem>> GetCourseFeedAsync(
        Guid courseId, ContentType? type, DateTimeOffset now, CancellationToken ct) =>
        FeedAsync(db.ContentLinks.Where(l => l.CourseId == courseId), type, now, ct);

    public Task<List<Comment>> GetCommentsAsync(Guid contentId, CancellationToken ct) =>
        db.Comments.Where(c => c.ContentId == contentId).OrderBy(c => c.CreatedAt).ToListAsync(ct);

    public void AddComment(Comment comment) => db.Comments.Add(comment);

    public void AddAttachment(Attachment attachment) => db.Attachments.Add(attachment);

    private Task<List<ContentFeedItem>> FeedAsync(
        IQueryable<ContentLink> links, ContentType? type, DateTimeOffset now, CancellationToken ct)
    {
        var q = from l in links
                join c in db.Content on l.ContentId equals c.Id
                where c.Status == ContentStatus.Published && l.PublishAt <= now
                select new { l, c };
        if (type is { } t)
        {
            q = q.Where(x => x.c.Type == t);
        }

        return q
            .OrderByDescending(x => x.l.IsPinned)
            .ThenByDescending(x => x.l.PublishAt)
            .Select(x => new ContentFeedItem(
                x.l.Id, x.c.Id, x.c.Type, x.c.Title, x.c.BodyMd, x.c.AuthorId,
                x.l.GroupId, x.l.CourseId, x.l.PublishAt, x.l.DueAt, x.l.IsPinned))
            .ToListAsync(ct);
    }
}
