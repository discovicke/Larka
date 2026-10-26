using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class ContentRepository(AppDb db) : IContentRepository
{
    public Task<Content?> GetAsync(Guid id, CancellationToken ct) =>
        db.Content.Include(content => content.Assignment).FirstOrDefaultAsync(content => content.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        db.Content.AnyAsync(content => content.Id == id, ct);

    public void Add(Content content) => db.Content.Add(content);

    public void AddLink(ContentLink link) => db.ContentLinks.Add(link);

    public async Task<LinkedContent?> GetLinkWithContentAsync(Guid linkId, CancellationToken ct)
    {
        var link = await db.ContentLinks.FirstOrDefaultAsync(contentLink => contentLink.Id == linkId, ct);
        if (link is null)
        {
            return null;
        }

        var content = await db.Content
            .Include(linkedContent => linkedContent.Assignment)
            .SingleAsync(linkedContent => linkedContent.Id == link.ContentId, ct);
        return new LinkedContent(link, content);
    }

    public Task<List<ContentFeedItem>> GetGroupFeedAsync(
        Guid groupId, ContentType? type, DateTimeOffset now, CancellationToken ct)
    {
        var courseIds = db.GroupCourses
            .Where(groupCourse => groupCourse.GroupId == groupId)
            .Select(groupCourse => groupCourse.CourseId);
        var links = db.ContentLinks.Where(link =>
            link.GroupId == groupId
            || (link.GroupId == null && link.CourseId != null && courseIds.Contains(link.CourseId.Value)));
        return FeedAsync(links, type, now, ct);
    }

    public Task<List<ContentFeedItem>> GetCourseFeedAsync(
        Guid courseId, ContentType? type, DateTimeOffset now, CancellationToken ct) =>
        FeedAsync(db.ContentLinks.Where(link => link.CourseId == courseId), type, now, ct);

    public Task<List<Comment>> GetCommentsAsync(Guid contentId, CancellationToken ct) =>
        db.Comments
            .Where(comment => comment.ContentId == contentId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(ct);

    public void AddComment(Comment comment) => db.Comments.Add(comment);

    public void AddAttachment(Attachment attachment) => db.Attachments.Add(attachment);

    private Task<List<ContentFeedItem>> FeedAsync(
        IQueryable<ContentLink> links, ContentType? type, DateTimeOffset now, CancellationToken ct)
    {
        var query = from link in links
                    join content in db.Content on link.ContentId equals content.Id
                    where content.Status == ContentStatus.Published && link.PublishAt <= now
                    select new { Link = link, Content = content };
        if (type is not null)
        {
            var contentType = type.Value;
            query = query.Where(row => row.Content.Type == contentType);
        }

        return query
            .OrderByDescending(row => row.Link.IsPinned)
            .ThenByDescending(row => row.Link.PublishAt)
            .Select(row => new ContentFeedItem(
                row.Link.Id, row.Content.Id, row.Content.Type, row.Content.Title, row.Content.BodyMd,
                row.Content.AuthorId, row.Link.GroupId, row.Link.CourseId, row.Link.ModuleId, row.Link.PublishAt,
                row.Link.DueAt, row.Link.IsPinned))
            .ToListAsync(ct);
    }
}
