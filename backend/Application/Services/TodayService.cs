using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Elevens Idag-sida. Bygger på det som redan finns: kalenderns länkar, elevvyns synlighet och klarregel
// samt gruppflödet för nyheter. Bara elevens egna grupper och egna inlämningar räknas.
public class TodayService(
    IGroupRepository groups,
    IContentRepository contents,
    IModuleRepository modules,
    ISubmissionRepository submissions,
    IPartCompletionRepository completions,
    IPermissions permissions,
    ICurrentUser currentUser,
    TimeProvider time)
{
    private const int _recentSubmissions = 30;
    private const int _listLength = 10;
    private const int _newsLength = 5;
    private const int _feedbackLength = 5;

    public async Task<TodayDto> GetAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var studentCourses = await groups.GetStudentCoursesAsync(currentUser.UserId, ct);
        var courseTitles = studentCourses.ToDictionary(course => course.Course.Id, course => course.Course.Title);

        var recent = await submissions.GetRecentForStudentAsync(currentUser.UserId, _recentSubmissions, ct);
        var recentItems = await LoadRecentAsync(recent, courseTitles, ct);

        return new TodayDto(
            await GetTodoAsync(now, ct),
            recentItems
                .Take(_listLength)
                .Select(item => new TodaySubmissionDto(
                    item.Link.Link.Id,
                    item.Link.Content.Title,
                    item.Link.Link.CourseId,
                    item.CourseTitle,
                    item.Submission.SubmittedAt,
                    item.Submission.Status,
                    item.Submission.Grade?.Level))
                .ToList(),
            recentItems
                .Where(item => item.Submission.Grade is not null)
                .OrderByDescending(item => item.Submission.Grade!.GradedAt)
                .Take(_feedbackLength)
                .Select(item => new TodayFeedbackDto(
                    item.Link.Link.Id,
                    item.Link.Content.Title,
                    item.Link.Link.CourseId,
                    item.CourseTitle,
                    item.Submission.Grade!.Level,
                    item.Submission.Grade.Feedback,
                    item.Submission.Grade.GradedAt))
                .ToList(),
            await GetNewsAsync(now, courseTitles, ct),
            await GetCoursesAsync(studentCourses, now, ct));
    }

    // Publicerade uppgifter i elevens grupper med deadline från nu, som eleven inte lämnat in eller klarmarkerat.
    private async Task<List<TodayTodoDto>> GetTodoAsync(DateTimeOffset now, CancellationToken ct)
    {
        var links = await contents.GetCalendarLinksAsync(currentUser.UserId, now, now.AddYears(10), ct);
        var candidates = new List<CalendarLink>();
        foreach (var link in links)
        {
            if (link.Content.Type == ContentType.Assignment
                && link.Link.DueAt >= now
                && ContentVisibility.IsVisible(link.Content, link.Link, now)
                && ContentVisibility.IsModuleAvailable(link.ModulePublishAt, now)
                && !await permissions.CanTeachLinkAsync(link.Link.Id, ct))
            {
                candidates.Add(link);
            }
        }

        var linkIds = candidates.Select(link => link.Link.Id).Distinct().ToList();
        var submissionByLink = (await submissions.GetForStudentAsync(currentUser.UserId, linkIds, ct))
            .ToDictionary(submission => submission.ContentLinkId);
        var completedLinks = (await completions.GetForStudentAsync(currentUser.UserId, linkIds, ct))
            .Select(completion => completion.ContentLinkId)
            .ToHashSet();

        return candidates
            .Where(link => PartRules.IsManual(link.Content)
                ? !completedLinks.Contains(link.Link.Id)
                : submissionByLink.GetValueOrDefault(link.Link.Id)?.Status is null
                    or SubmissionStatus.NotStarted
                    or SubmissionStatus.NeedsWork)
            .OrderBy(link => link.Link.DueAt)
            .ThenBy(link => link.Content.Title)
            .Select(link => new TodayTodoDto(
                link.Link.Id,
                link.Content.Id,
                link.Content.Title,
                link.Link.CourseId,
                link.CourseTitle,
                link.Link.DueAt!.Value,
                submissionByLink.GetValueOrDefault(link.Link.Id)?.Status ?? SubmissionStatus.NotStarted))
            .ToList();
    }

    private async Task<List<RecentSubmission>> LoadRecentAsync(
        List<Submission> recent, Dictionary<Guid, string> courseTitles, CancellationToken ct)
    {
        var items = new List<RecentSubmission>();
        foreach (var submission in recent)
        {
            var link = await contents.GetLinkWithContentAsync(submission.ContentLinkId, ct);
            if (link is null || link.Content.Status != ContentStatus.Published)
            {
                continue;
            }

            var courseTitle = link.Link.CourseId is null
                ? null
                : courseTitles.GetValueOrDefault(link.Link.CourseId.Value);
            items.Add(new RecentSubmission(submission, link, courseTitle));
        }

        return items;
    }

    // Nyheter från elevens grupper och kurserna grupperna läser. En rad per nyhet.
    private async Task<List<TodayNewsDto>> GetNewsAsync(
        DateTimeOffset now, Dictionary<Guid, string> courseTitles, CancellationToken ct)
    {
        var feed = new List<ContentFeedItem>();
        var groupNames = new Dictionary<Guid, string>();
        foreach (var groupId in await groups.GetStudentGroupIdsAsync(currentUser.UserId, ct))
        {
            var group = await groups.GetAsync(groupId, ct);
            groupNames[groupId] = group?.Name ?? "";
            feed.AddRange(await contents.GetGroupFeedAsync(groupId, ContentType.News, now, ct));
        }

        return feed
            .GroupBy(item => item.ContentId)
            .Select(items => items.OrderByDescending(item => item.PublishAt).First())
            .OrderByDescending(item => item.PublishAt)
            .Take(_newsLength)
            .Select(item => new TodayNewsDto(
                item.ContentId,
                item.Title,
                item.CourseId,
                SourceOf(item, courseTitles, groupNames),
                item.PublishAt))
            .ToList();
    }

    private static string? SourceOf(
        ContentFeedItem item, Dictionary<Guid, string> courseTitles, Dictionary<Guid, string> groupNames)
    {
        if (item.CourseId is not null && courseTitles.TryGetValue(item.CourseId.Value, out var course))
        {
            return course;
        }

        return item.GroupId is not null && groupNames.TryGetValue(item.GroupId.Value, out var group)
            ? group
            : null;
    }

    // Antal delar eleven klarat i varje kurs, räknat på det eleven ser i elevvyn (utan nyheter).
    private async Task<List<TodayCourseDto>> GetCoursesAsync(
        List<StudentCourse> studentCourses, DateTimeOffset now, CancellationToken ct)
    {
        var result = new List<TodayCourseDto>();
        foreach (var studentCourse in studentCourses
                     .OrderBy(course => course.Course.StartsOn is null
                         ? 1
                         : 0)
                     .ThenBy(course => course.Course.StartsOn)
                     .ThenBy(course => course.Course.Title))
        {
            var courseModules = await modules.GetForCourseAsync(studentCourse.Course.Id, ct);
            var availableModuleIds = courseModules
                .Where(module => ContentVisibility.IsModuleAvailable(module.PublishAt, now))
                .Select(module => module.Id)
                .ToHashSet();

            var parts = new List<LinkedContent>();
            foreach (var groupId in studentCourse.GroupIds)
            {
                parts.AddRange(await contents.GetLinksWithContentForGroupCourseAsync(
                    groupId, studentCourse.Course.Id, ct));
            }

            var visible = parts
                .Where(part => part.Link.ModuleId is not null
                    && availableModuleIds.Contains(part.Link.ModuleId.Value)
                    && ContentVisibility.IsVisible(part.Content, part.Link, now)
                    && part.Content.Type != ContentType.News)
                .DistinctBy(part => part.Link.Id)
                .ToList();

            var linkIds = visible.Select(part => part.Link.Id).ToList();
            var completionByLink = (await completions.GetForStudentAsync(currentUser.UserId, linkIds, ct))
                .ToDictionary(completion => completion.ContentLinkId);
            var submissionByLink = (await submissions.GetForStudentAsync(currentUser.UserId, linkIds, ct))
                .ToDictionary(submission => submission.ContentLinkId);

            result.Add(new TodayCourseDto(
                studentCourse.Course.Id,
                studentCourse.Course.Code,
                studentCourse.Course.Title,
                visible.Count,
                visible.Count(part => PartRules.IsSatisfied(
                    part.Content,
                    completionByLink.GetValueOrDefault(part.Link.Id),
                    submissionByLink.GetValueOrDefault(part.Link.Id)))));
        }

        return result;
    }

    private record RecentSubmission(Submission Submission, LinkedContent Link, string? CourseTitle);
}
