using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class ContentRepository(AppDb db) : IContentRepository
{
    public Task<Content?> GetAsync(Guid id, CancellationToken ct) =>
        db.Content
            .Include(content => content.Assignment)
            .ThenInclude(assignment => assignment!.Requirements)
            .FirstOrDefaultAsync(content => content.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        db.Content.AnyAsync(content => content.Id == id, ct);

    public Task<bool> IsTeacherOfContentAsync(Guid contentId, Guid userId, CancellationToken ct)
    {
        var teacherGroupIds = db.Enrollments
            .Where(enrollment => enrollment.UserId == userId && enrollment.Role == GroupRole.Teacher)
            .Select(enrollment => enrollment.GroupId);
        var taughtCourseIds = db.CourseTeachers
            .Where(courseTeacher => courseTeacher.UserId == userId)
            .Select(courseTeacher => courseTeacher.CourseId);

        // En länk med kurs avgörs av kursens lärare, en länk till bara en grupp av lärarrollen i gruppen.
        return db.ContentLinks
            .Where(link => link.ContentId == contentId)
            .AnyAsync(link =>
                (link.CourseId == null && link.GroupId != null && teacherGroupIds.Contains(link.GroupId.Value))
                || (link.CourseId != null && taughtCourseIds.Contains(link.CourseId.Value))
                || (link.ModuleId != null && db.Modules.Any(module =>
                    module.Id == link.ModuleId && taughtCourseIds.Contains(module.CourseId))),
                ct);
    }

    public Task<bool> IsVisibleToMemberOfContentAsync(
        Guid contentId, Guid userId, DateTimeOffset now, CancellationToken ct) =>
        VisibleToMember(db.ContentLinks.Where(link => link.ContentId == contentId), userId, now).AnyAsync(ct);

    public Task<bool> IsVisibleToMemberOfLinkAsync(
        Guid linkId, Guid userId, DateTimeOffset now, CancellationToken ct) =>
        VisibleToMember(db.ContentLinks.Where(link => link.Id == linkId), userId, now).AnyAsync(ct);

    // Länkarna som är publicerade och når en grupp där användaren är elev.
    private IQueryable<ContentLink> VisibleToMember(IQueryable<ContentLink> links, Guid userId, DateTimeOffset now)
    {
        // Elev i gruppen. Lärarrollen i en grupp ger ingen läsrätt här, den prövas som undervisning.
        var myGroupIds = db.Enrollments
            .Where(enrollment => enrollment.UserId == userId && enrollment.Role != GroupRole.Teacher)
            .Select(enrollment => enrollment.GroupId);

        return links
            .Where(link => link.PublishAt <= now)
            .Where(link => db.Content.Any(content =>
                content.Id == link.ContentId && content.Status == ContentStatus.Published))
            .Where(link => link.ModuleId == null
                || !db.Modules.Any(module => module.Id == link.ModuleId
                    && module.PublishAt != null
                    && module.PublishAt > now))
            .Where(link =>
                (link.CourseId == null && link.GroupId != null && myGroupIds.Contains(link.GroupId.Value))
                || (link.CourseId != null && db.GroupCourses.Any(groupCourse =>
                    groupCourse.CourseId == link.CourseId
                    && myGroupIds.Contains(groupCourse.GroupId)
                    && (link.GroupId == null || link.GroupId == groupCourse.GroupId))));
    }

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

    public async Task<LinkedContent?> GetLinkWithContentAsync(
        Guid contentId, Guid linkId, CancellationToken ct)
    {
        var link = await db.ContentLinks.FirstOrDefaultAsync(
            contentLink => contentLink.Id == linkId && contentLink.ContentId == contentId, ct);
        if (link is null)
        {
            return null;
        }

        var content = await db.Content
            .Include(linkedContent => linkedContent.Assignment)
            .SingleAsync(linkedContent => linkedContent.Id == contentId, ct);
        return new LinkedContent(link, content);
    }

    public Task<List<AssignmentReference>> GetReferencesForSourceAsync(
        Guid sourceLinkId, CancellationToken ct) =>
        db.AssignmentReferences
            .Where(reference => reference.SourceLinkId == sourceLinkId)
            .OrderBy(reference => reference.Position)
            .ToListAsync(ct);

    public Task<List<RelatedLinkRow>> GetRelatedLinksForSourcesAsync(
        IReadOnlyCollection<Guid> sourceLinkIds, CancellationToken ct)
    {
        if (sourceLinkIds.Count == 0)
        {
            return Task.FromResult<List<RelatedLinkRow>>([]);
        }

        return (from reference in db.AssignmentReferences
                join target in db.ContentLinks on reference.TargetLinkId equals target.Id
                join content in db.Content on target.ContentId equals content.Id
                join module in db.Modules on target.ModuleId equals module.Id into modules
                from module in modules.DefaultIfEmpty()
                where sourceLinkIds.Contains(reference.SourceLinkId)
                orderby reference.SourceLinkId, reference.Position
                select new RelatedLinkRow(
                    reference.SourceLinkId,
                    new RelatedLinkDto(
                        target.Id,
                        content.Id,
                        content.Title,
                        content.Type,
                        target.ModuleId,
                        module != null
                            ? module.Title
                            : null,
                        target.CourseId)))
            .ToListAsync(ct);
    }

    public void AddReferences(IEnumerable<AssignmentReference> references) =>
        db.AssignmentReferences.AddRange(references);

    public void RemoveReferences(IEnumerable<AssignmentReference> references) =>
        db.AssignmentReferences.RemoveRange(references);

    public async Task<int> GetNextLinkPositionAsync(Guid moduleId, CancellationToken ct)
    {
        var lastPosition = await db.ContentLinks
            .Where(link => link.ModuleId == moduleId)
            .MaxAsync(link => (int?)link.Position, ct);
        return (lastPosition ?? 0) + 1;
    }

    public Task<List<ContentLink>> GetLinksForModuleAsync(Guid moduleId, CancellationToken ct) =>
        db.ContentLinks
            .Where(link => link.ModuleId == moduleId)
            .OrderBy(link => link.Position)
            .ThenBy(link => link.PublishAt)
            .ThenBy(link => link.Id)
            .ToListAsync(ct);

    public async Task<List<LinkedContent>> GetLinksWithContentForModuleAsync(
        Guid moduleId, CancellationToken ct)
    {
        var links = await db.ContentLinks
            .Where(link => link.ModuleId == moduleId)
            .OrderBy(link => link.Position)
            .ThenBy(link => link.PublishAt)
            .ThenBy(link => link.Id)
            .ToListAsync(ct);
        return await WithContentAsync(links, ct);
    }

    public async Task<List<LinkedContent>> GetLinksWithContentForCourseAsync(
        Guid courseId, CancellationToken ct)
    {
        var links = await db.ContentLinks
            .Where(link => link.CourseId == courseId)
            .OrderBy(link => link.Id)
            .ToListAsync(ct);
        return await WithContentAsync(links, ct);
    }

    public async Task<List<LinkedContent>> GetLinksWithContentForGroupCourseAsync(
        Guid groupId, Guid courseId, CancellationToken ct)
    {
        var links = await db.ContentLinks
            .Where(link => link.CourseId == courseId
                && (link.GroupId == groupId || link.GroupId == null)
                && link.ModuleId != null)
            .OrderBy(link => link.Position)
            .ThenBy(link => link.PublishAt)
            .ThenBy(link => link.Id)
            .ToListAsync(ct);
        return await WithContentAsync(links, ct);
    }

    public Task<List<ContentLinkDependency>> GetDependenciesForModuleAsync(
        Guid moduleId, CancellationToken ct) =>
        db.ContentLinkDependencies
            .Where(dependency => db.ContentLinks.Any(link =>
                link.Id == dependency.BlockedLinkId && link.ModuleId == moduleId))
            .ToListAsync(ct);

    public Task<List<ContentLinkDependency>> GetDependenciesForCourseAsync(
        Guid courseId, CancellationToken ct) =>
        db.ContentLinkDependencies
            .Where(dependency => db.ContentLinks.Any(link =>
                link.Id == dependency.BlockedLinkId && link.CourseId == courseId))
            .ToListAsync(ct);

    public Task<List<CalendarLink>> GetCalendarLinksAsync(
        Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var groupIds = db.Enrollments
            .Where(enrollment => enrollment.UserId == userId)
            .Select(enrollment => enrollment.GroupId);
        // Lärarrollen i en klass ger bara klassens egna nyheter, inte kursernas innehåll.
        var courseGroupIds = db.Enrollments
            .Where(enrollment => enrollment.UserId == userId && enrollment.Role != GroupRole.Teacher)
            .Select(enrollment => enrollment.GroupId);
        var courseIds = db.GroupCourses
            .Where(groupCourse => courseGroupIds.Contains(groupCourse.GroupId))
            .Select(groupCourse => groupCourse.CourseId);
        var taughtCourseIds = db.CourseTeachers
            .Where(courseTeacher => courseTeacher.UserId == userId)
            .Select(courseTeacher => courseTeacher.CourseId);
        var teacherGroupIds = db.Enrollments
            .Where(enrollment => enrollment.UserId == userId && enrollment.Role == GroupRole.Teacher)
            .Select(enrollment => enrollment.GroupId);

        // Kursens lärare ser kursens alla länkar, även de riktade till en enskild klass, utan att vara med i klassen.
        // En del publiceras när både länken och veckan har öppnat, så veckans datum kan lägga publiceringen i perioden.
        return db.ContentLinks
            .Where(link =>
                (link.CourseId == null && link.GroupId != null && groupIds.Contains(link.GroupId.Value))
                || (link.CourseId != null && link.GroupId != null && courseGroupIds.Contains(link.GroupId.Value))
                || (link.GroupId == null && link.CourseId != null && courseIds.Contains(link.CourseId.Value))
                || (link.CourseId != null && taughtCourseIds.Contains(link.CourseId.Value)))
            .Where(link => link.ModuleId != null || link.DueAt != null)
            .Where(link =>
                (link.DueAt != null && link.DueAt >= from && link.DueAt < to)
                || (link.DueAt != null && link.ResubmitDueAt.Any(due => due >= from && due < to))
                || db.Submissions.Any(submission => submission.ContentLinkId == link.Id
                    && submission.StudentId == userId
                    && submission.PersonalDueAt >= from
                    && submission.PersonalDueAt < to)
                || (link.PublishAt >= from && link.PublishAt < to)
                || db.Modules.Any(module => module.Id == link.ModuleId
                    && module.PublishAt >= from
                    && module.PublishAt < to))
            .Join(
                db.Content,
                link => link.ContentId,
                content => content.Id,
                (link, content) => new { Link = link, Content = content })
            .Select(row => new CalendarLink(
                row.Link,
                row.Content,
                row.Link.CourseId == null
                    ? null
                    : db.Courses
                        .Where(course => course.Id == row.Link.CourseId.Value)
                        .Select(course => course.Title)
                        .FirstOrDefault(),
                row.Link.ModuleId == null
                    ? null
                    : db.Modules
                        .Where(module => module.Id == row.Link.ModuleId.Value)
                        .Select(module => (DateTimeOffset?)module.PublishAt)
                        .FirstOrDefault(),
                db.Submissions
                    .Where(submission => submission.ContentLinkId == row.Link.Id && submission.StudentId == userId)
                    .Select(submission => submission.PersonalDueAt)
                    .FirstOrDefault(),
                row.Link.CourseId != null
                    ? taughtCourseIds.Contains(row.Link.CourseId.Value)
                    : row.Link.GroupId != null && teacherGroupIds.Contains(row.Link.GroupId.Value)))
            .ToListAsync(ct);
    }

    public async Task<HashSet<Guid>> GetCourseIdsWithPublishedPartsAsync(
        IReadOnlyCollection<Guid> courseIds, CancellationToken ct) =>
        (await (from link in db.ContentLinks
                join content in db.Content on link.ContentId equals content.Id
                where link.CourseId != null
                    && courseIds.Contains(link.CourseId.Value)
                    && link.ModuleId != null
                    && content.Status == ContentStatus.Published
                select link.CourseId!.Value)
            .Distinct()
            .ToListAsync(ct))
        .ToHashSet();

    public Task<bool> AnyRepoAssignmentAsync(
        IReadOnlyCollection<Guid> courseIds,
        IReadOnlyCollection<Guid> groupIds,
        DateTimeOffset now,
        CancellationToken ct) =>
        (from link in db.ContentLinks
         join content in db.Content on link.ContentId equals content.Id
         where ((link.CourseId != null && courseIds.Contains(link.CourseId.Value))
                 || (link.GroupId != null && groupIds.Contains(link.GroupId.Value)))
             && content.Status == ContentStatus.Published
             && link.PublishAt <= now
             && content.Assignment != null
             && content.Assignment.SubmissionModes.Contains(SubmissionMode.Repo)
         select link.Id)
        .AnyAsync(ct);

    public Task<List<AssignmentGroup>> GetGroupsForLinkAsync(Guid linkId, CancellationToken ct) =>
        db.AssignmentGroups
            .Where(group => group.ContentLinkId == linkId)
            .Include(group => group.Members)
                .ThenInclude(member => member.User)
            .Include(group => group.Repo)
            .OrderBy(group => group.Position)
            .ToListAsync(ct);

    public async Task<List<CourseDeadline>> GetCourseDeadlinesAsync(
        IReadOnlyCollection<Guid> courseIds, DateTimeOffset fromAt, DateTimeOffset toAt, CancellationToken ct)
    {
        var rows = await (
                from link in db.ContentLinks
                join content in db.Content on link.ContentId equals content.Id
                where link.CourseId != null
                    && courseIds.Contains(link.CourseId.Value)
                    && content.Type == ContentType.Assignment
                    && content.Status == ContentStatus.Published
                    // Bara uppgifter som tar emot inlämningar, och bara delar eleverna redan ser: länkens och veckans
                    // publicering ska ha passerat. Samma urval som veckolistans räknare.
                    && content.Assignment != null
                    && content.Assignment.SubmissionModes.Count > 0
                    && link.PublishAt <= fromAt
                    && (link.ModuleId == null
                        || !db.Modules.Any(module => module.Id == link.ModuleId
                            && module.PublishAt != null
                            && module.PublishAt > fromAt))
                    && link.DueAt != null
                    && ((link.DueAt >= fromAt && link.DueAt <= toAt)
                        || link.ResubmitDueAt.Any(due => due >= fromAt && due <= toAt)
                        || db.Submissions.Any(submission => submission.ContentLinkId == link.Id
                            && submission.Status != SubmissionStatus.Graded
                            && submission.PersonalDueAt >= fromAt
                            && submission.PersonalDueAt <= toAt))
                select new
                {
                    LinkId = link.Id,
                    content.Title,
                    CourseId = link.CourseId!.Value,
                    CourseTitle = db.Courses
                        .Where(course => course.Id == link.CourseId.Value)
                        .Select(course => course.Title)
                        .First(),
                    DueAt = link.DueAt!.Value,
                    link.ResubmitDueAt,
                    NeedsWork = db.Submissions.Count(submission => submission.ContentLinkId == link.Id
                        && submission.Status == SubmissionStatus.NeedsWork),
                    Personal = db.Submissions
                        .Where(submission => submission.ContentLinkId == link.Id
                            && submission.Status != SubmissionStatus.Graded
                            && submission.PersonalDueAt >= fromAt
                            && submission.PersonalDueAt <= toAt)
                        .Select(submission => submission.PersonalDueAt!.Value)
                        .ToList()
                })
            .ToListAsync(ct);

        // En rad per deadline: uppgiftens egen, varje kompletteringsdeadline och elevernas egna deadlines, där
        // elever med samma tid blir en rad.
        return rows
            .SelectMany(row =>
            {
                var deadlines = new List<CourseDeadline>();
                if (row.DueAt >= fromAt && row.DueAt <= toAt)
                {
                    deadlines.Add(new CourseDeadline(row.LinkId, row.Title, row.CourseId, row.CourseTitle, row.DueAt));
                }

                deadlines.AddRange(row.ResubmitDueAt
                    .Where(due => due >= fromAt && due <= toAt)
                    .Select(due => new CourseDeadline(
                        row.LinkId,
                        row.Title,
                        row.CourseId,
                        row.CourseTitle,
                        due,
                        CalendarItemKind.ResubmitDeadline,
                        row.NeedsWork)));
                deadlines.AddRange(row.Personal
                    .GroupBy(due => due)
                    .Select(group => new CourseDeadline(
                        row.LinkId,
                        row.Title,
                        row.CourseId,
                        row.CourseTitle,
                        group.Key,
                        CalendarItemKind.PersonalDeadline,
                        group.Count())));
                return deadlines;
            })
            .OrderBy(deadline => deadline.DueAt)
            .ThenBy(deadline => deadline.Title)
            .ToList();
    }

    public Task<List<ScheduledContentLink>> GetScheduledLinksAsync(
        IReadOnlyCollection<Guid> courseIds, DateTimeOffset fromAt, DateTimeOffset toAt, CancellationToken ct) =>
        (from link in db.ContentLinks
         join content in db.Content on link.ContentId equals content.Id
         where link.CourseId != null
             && courseIds.Contains(link.CourseId.Value)
             && content.Status == ContentStatus.Published
             && link.PublishAt > fromAt
             && link.PublishAt <= toAt
         orderby link.PublishAt, content.Title
         select new ScheduledContentLink(
             link.Id,
             content.Title,
             link.CourseId!.Value,
             db.Courses
                 .Where(course => course.Id == link.CourseId.Value)
                 .Select(course => course.Title)
                 .First(),
             link.PublishAt))
        .ToListAsync(ct);

    public Task<List<ScheduledCourseModule>> GetScheduledModulesAsync(
        IReadOnlyCollection<Guid> courseIds, DateTimeOffset fromAt, DateTimeOffset toAt, CancellationToken ct) =>
        (from module in db.Modules
         join course in db.Courses on module.CourseId equals course.Id
         where courseIds.Contains(module.CourseId)
             && module.PublishAt != null
             && module.PublishAt > fromAt
             && module.PublishAt <= toAt
         orderby module.PublishAt, module.Title
         select new ScheduledCourseModule(
             module.Id,
             module.Title,
             course.Id,
             course.Title,
             module.PublishAt!.Value))
        .ToListAsync(ct);

    public void AddDependency(ContentLinkDependency dependency) => db.ContentLinkDependencies.Add(dependency);

    public void RemoveDependencies(IEnumerable<ContentLinkDependency> dependencies) =>
        db.ContentLinkDependencies.RemoveRange(dependencies);

    public Task<int> CountLinksAsync(Guid contentId, CancellationToken ct) =>
        db.ContentLinks.CountAsync(link => link.ContentId == contentId, ct);

    public void RemoveLink(ContentLink link) => db.ContentLinks.Remove(link);

    public void RemoveContent(Content content) => db.Content.Remove(content);

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

    public Task<Comment?> GetCommentAsync(Guid contentId, Guid commentId, CancellationToken ct) =>
        db.Comments.FirstOrDefaultAsync(comment => comment.ContentId == contentId && comment.Id == commentId, ct);

    public void RemoveComment(Comment comment) => db.Comments.Remove(comment);

    public void AddAttachment(Attachment attachment) => db.Attachments.Add(attachment);

    public Task<List<Attachment>> GetAttachmentsAsync(Guid contentId, CancellationToken ct) =>
        db.Attachments
            .Where(attachment => attachment.ContentId == contentId)
            .OrderBy(attachment => attachment.FileName)
            .ToListAsync(ct);

    public Task<List<Attachment>> GetAttachmentsAsync(IReadOnlyCollection<Guid> contentIds, CancellationToken ct) =>
        db.Attachments
            .Where(attachment => contentIds.Contains(attachment.ContentId))
            .OrderBy(attachment => attachment.FileName)
            .ToListAsync(ct);

    public Task<Attachment?> GetAttachmentAsync(Guid contentId, Guid attachmentId, CancellationToken ct) =>
        db.Attachments.FirstOrDefaultAsync(
            attachment => attachment.ContentId == contentId && attachment.Id == attachmentId, ct);

    public void RemoveAttachment(Attachment attachment) => db.Attachments.Remove(attachment);

    // Innehållet hämtas i en egen fråga, så att AssignmentSpec följer med och svaret blir en rad per länk.
    private async Task<List<LinkedContent>> WithContentAsync(
        List<ContentLink> links, CancellationToken ct)
    {
        if (links.Count == 0)
        {
            return [];
        }

        var contentIds = links.Select(link => link.ContentId).Distinct().ToList();
        var contents = await db.Content
            .Include(content => content.Assignment)
                .ThenInclude(assignment => assignment!.Requirements)
            .Where(content => contentIds.Contains(content.Id))
            .ToDictionaryAsync(content => content.Id, ct);
        return links
            .Select(link => new LinkedContent(link, contents[link.ContentId]))
            .ToList();
    }

    private Task<List<ContentFeedItem>> FeedAsync(
        IQueryable<ContentLink> links, ContentType? type, DateTimeOffset now, CancellationToken ct)
    {
        var query = from link in links
                    join content in db.Content on link.ContentId equals content.Id
                    join author in db.Users on content.AuthorId equals author.Id
                    where content.Status == ContentStatus.Published && link.PublishAt <= now
                    // Veckans datum gäller även flödena, så att en nyhet i en framtida vecka inte läcker.
                    where link.ModuleId == null
                        || !db.Modules.Any(module => module.Id == link.ModuleId
                            && module.PublishAt != null
                            && module.PublishAt > now)
                    select new { Link = link, Content = content, AuthorName = author.Name, author.AvatarKey };
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
                row.Content.AuthorId, row.AuthorName, row.Link.GroupId, row.Link.CourseId, row.Link.ModuleId,
                row.Link.PublishAt, row.Link.DueAt, row.Link.IsPinned,
                AvatarPolicy.UrlFor(row.Content.AuthorId, row.AvatarKey)))
            .ToListAsync(ct);
    }
}
