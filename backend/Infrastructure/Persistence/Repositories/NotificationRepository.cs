using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class NotificationRepository(AppDb db) : INotificationRepository
{
    // En länk med sitt innehåll och veckans publiceringsdatum, för frågorna om vad som är synligt.
    private sealed class VisibleLink
    {
        public required ContentLink Link { get; init; }
        public required Content Content { get; init; }
        public DateTimeOffset? ModulePublishAt { get; init; }
    }

    public void AddRange(IEnumerable<Notification> notifications) => db.Notifications.AddRange(notifications);

    public Task<List<Guid>> GetMemberIdsAsync(
        IReadOnlyCollection<Guid> groupIds, bool includeTeachers, CancellationToken ct) =>
        db.Enrollments
            .Where(enrollment => groupIds.Contains(enrollment.GroupId)
                && (includeTeachers || enrollment.Role != GroupRole.Teacher))
            .Select(enrollment => enrollment.UserId)
            .Distinct()
            .ToListAsync(ct);

    public Task<List<Guid>> GetCourseTeacherIdsAsync(Guid courseId, CancellationToken ct) =>
        db.CourseTeachers
            .Where(courseTeacher => courseTeacher.CourseId == courseId)
            .Select(courseTeacher => courseTeacher.UserId)
            .ToListAsync(ct);

    public Task<List<Guid>> GetStudentIdsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct) =>
        db.Enrollments
            .Where(enrollment => groupIds.Contains(enrollment.GroupId) && enrollment.Role == GroupRole.Student)
            .Select(enrollment => enrollment.UserId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<HashSet<Guid>> GetNotifiedUserIdsAsync(
        NotificationKind kind, Guid contentId, CancellationToken ct) =>
        (await db.Notifications
            .Where(notification => notification.Kind == kind && notification.ContentId == contentId)
            .Select(notification => notification.UserId)
            .ToListAsync(ct))
        .ToHashSet();

    public Task<List<ContentLink>> GetLinksAsync(Guid contentId, CancellationToken ct) =>
        db.ContentLinks.Where(link => link.ContentId == contentId).ToListAsync(ct);

    public async Task<List<LinkedContent>> GetPublishedBetweenAsync(
        DateTimeOffset since, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await VisibleLinks(now)
            .Where(row => row.Content.Type == ContentType.News || row.Content.Type == ContentType.Assignment)
            .Where(row => row.Link.PublishAt > since || row.ModulePublishAt > since)
            .ToListAsync(ct);
        return rows.Select(row => new LinkedContent(row.Link, row.Content)).ToList();
    }

    public async Task<List<LinkedContent>> GetDeadlinesBetweenAsync(
        DateTimeOffset now, DateTimeOffset until, CancellationToken ct)
    {
        var rows = await VisibleLinks(now)
            .Where(row => row.Content.Type == ContentType.Assignment)
            .Where(row => row.Link.DueAt > now && row.Link.DueAt <= until)
            .ToListAsync(ct);
        return rows.Select(row => new LinkedContent(row.Link, row.Content)).ToList();
    }

    public async Task<List<NotificationRow>> GetForUserAsync(Guid userId, int take, CancellationToken ct)
    {
        var items = await db.Notifications
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenBy(notification => notification.Id)
            .Take(take)
            .ToListAsync(ct);

        var contentIds = items.Where(item => item.ContentId != null).Select(item => item.ContentId!.Value)
            .Distinct().ToList();
        var submissionIds = items.Where(item => item.SubmissionId != null).Select(item => item.SubmissionId!.Value)
            .Distinct().ToList();

        var titles = await db.Content
            .Where(content => contentIds.Contains(content.Id))
            .ToDictionaryAsync(content => content.Id, content => content.Title, ct);
        var graded = await db.Submissions
            .Where(submission => submissionIds.Contains(submission.Id))
            .Select(submission => new
            {
                submission.Id,
                submission.ContentLinkId,
                Level = submission.Grade == null
                    ? null
                    : submission.Grade.Level
            })
            .ToDictionaryAsync(submission => submission.Id, ct);

        // Innehållets länkar som användaren själv nås av, eller länken inlämningen gäller.
        var groupIds = db.Enrollments.Where(enrollment => enrollment.UserId == userId)
            .Select(enrollment => enrollment.GroupId);
        var courseIds = db.GroupCourses.Where(groupCourse => groupIds.Contains(groupCourse.GroupId))
            .Select(groupCourse => groupCourse.CourseId)
            .Concat(db.CourseTeachers.Where(courseTeacher => courseTeacher.UserId == userId)
                .Select(courseTeacher => courseTeacher.CourseId));
        var submissionLinkIds = graded.Values.Select(submission => submission.ContentLinkId).ToList();
        var links = await db.ContentLinks
            .Where(link => submissionLinkIds.Contains(link.Id)
                || (contentIds.Contains(link.ContentId)
                    && ((link.GroupId != null && groupIds.Contains(link.GroupId.Value))
                        || (link.GroupId == null && link.CourseId != null
                            && courseIds.Contains(link.CourseId.Value)))))
            .OrderBy(link => link.PublishAt)
            .ToListAsync(ct);

        var courseTitles = await db.Courses
            .Where(course => links.Select(link => link.CourseId).Contains(course.Id))
            .ToDictionaryAsync(course => course.Id, course => course.Title, ct);
        var groupNames = await db.Groups
            .Where(group => links.Select(link => link.GroupId).Contains(group.Id))
            .ToDictionaryAsync(group => group.Id, group => group.Name, ct);

        return items.Select(item =>
        {
            var link = item.SubmissionId != null && graded.TryGetValue(item.SubmissionId.Value, out var submission)
                ? links.FirstOrDefault(candidate => candidate.Id == submission.ContentLinkId)
                : links.FirstOrDefault(candidate => candidate.ContentId == item.ContentId);
            var title = item.ContentId != null && titles.TryGetValue(item.ContentId.Value, out var found)
                ? found
                : null;
            var courseTitle = link?.CourseId != null && courseTitles.TryGetValue(link.CourseId.Value, out var course)
                ? course
                : null;
            var groupName = link?.GroupId != null && groupNames.TryGetValue(link.GroupId.Value, out var name)
                ? name
                : null;
            var level = item.SubmissionId != null && graded.TryGetValue(item.SubmissionId.Value, out var gradedOne)
                ? gradedOne.Level
                : null;
            return new NotificationRow(item, title, link?.Id, link?.CourseId, courseTitle, groupName, level);
        }).ToList();
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct) =>
        db.Notifications.CountAsync(notification => notification.UserId == userId && notification.ReadAt == null, ct);

    public Task<Notification?> GetAsync(Guid id, CancellationToken ct) =>
        db.Notifications.FirstOrDefaultAsync(notification => notification.Id == id, ct);

    public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken ct) =>
        db.Notifications
            .Where(notification => notification.UserId == userId && notification.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAt, readAt), ct);

    // Länkar till publicerat innehåll som passerat sin publicering, i en vecka som är öppen.
    private IQueryable<VisibleLink> VisibleLinks(DateTimeOffset now) =>
        db.ContentLinks
            .Where(link => link.PublishAt <= now)
            .Join(
                db.Content
                    .Include(content => content.Assignment)
                    .Where(content => content.Status == ContentStatus.Published),
                link => link.ContentId,
                content => content.Id,
                (link, content) => new VisibleLink
                {
                    Link = link,
                    Content = content,
                    ModulePublishAt = link.ModuleId == null
                        ? null
                        : db.Modules
                            .Where(module => module.Id == link.ModuleId.Value)
                            .Select(module => module.PublishAt)
                            .FirstOrDefault()
                })
            .Where(row => row.ModulePublishAt == null || row.ModulePublishAt <= now);
}
