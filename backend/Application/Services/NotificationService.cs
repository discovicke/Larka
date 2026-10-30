using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Notiser till användarna: skapas när något publiceras eller bedöms, och läses i inkorgen.
// Metoderna som skapar notiser (Notify...) sparar inte själva. Anroparen sparar i samma transaktion som
// ändringen som gav notisen. CreateScheduledAsync, som jobbet anropar, sparar däremot själv.
// En nyhet eller uppgift ger högst en notis per användare (NewsPublished, AssignmentPublished,
// DeadlineSoon), så att samma sak aldrig dyker upp två gånger.
public class NotificationService(
    INotificationRepository notifications,
    IGroupRepository groups,
    ICourseRepository courses,
    IUserRepository users,
    IModuleRepository modules,
    ISubmissionRepository submissions,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time)
{
    private const int _defaultLimit = 50;
    private const int _maxLimit = 200;

    // Jobbet tittar så här långt bakåt efter publiceringar det missat, t.ex. om servern legat nere.
    private static readonly TimeSpan _publishLookback = TimeSpan.FromDays(3);
    private static readonly TimeSpan _deadlineWarning = TimeSpan.FromHours(24);

    /// <summary>
    /// Notis till medlemmarna i länkens grupper om att en nyhet eller uppgift publicerats, utom till författaren.
    /// Gör ingenting om länken inte är synlig än (utkast, PublishAt i framtiden, veckan stängd). Då tar jobbet
    /// över när tiden passerat.
    /// </summary>
    public Task NotifyLinkPublishedAsync(Content content, ContentLink link, CancellationToken ct) =>
        NotifyPublishedAsync(content, [link], ct);

    /// <summary>Samma sak för alla länkar till innehållet, när ett utkast publiceras.</summary>
    public async Task NotifyContentPublishedAsync(Content content, CancellationToken ct) =>
        await NotifyPublishedAsync(content, await notifications.GetLinksAsync(content.Id, ct), ct);

    /// <summary>Notis till eleven om att inlämningen bedömts (Graded) eller behöver kompletteras (NeedsWork).</summary>
    public async Task NotifySubmissionAsync(
        Submission submission, Guid contentId, NotificationKind kind, CancellationToken ct)
    {
        if (await IsMutedAsync(submission.StudentId, kind, ct))
        {
            return;
        }

        notifications.AddRange([
            new Notification
            {
                UserId = submission.StudentId,
                Kind = kind,
                ContentId = contentId,
                SubmissionId = submission.Id,
                CreatedAt = time.GetUtcNow()
            }
        ]);
    }

    /// <summary>Notis till lärarna på utdelningen när en elev själv ber om hjälp.</summary>
    public async Task NotifyStudentHelpRequestedAsync(
        ContentLink link, Guid contentId, Guid? submissionId, CancellationToken ct)
    {
        if (link.CourseId is null)
        {
            return;
        }

        var teacherIds = (await courses.GetTeachersAsync(link.CourseId.Value, ct))
            .Select(teacher => teacher.UserId)
            .Distinct()
            .ToList();
        if (teacherIds.Count == 0)
        {
            return;
        }

        var muted = await MutedAmongAsync(
            teacherIds.ToHashSet(), NotificationKind.StudentHelpRequested, ct);
        notifications.AddRange(teacherIds
            .Where(teacherId => !muted.Contains(teacherId))
            .Select(teacherId => new Notification
            {
                UserId = teacherId,
                Kind = NotificationKind.StudentHelpRequested,
                ContentId = contentId,
                SubmissionId = submissionId,
                CreatedAt = time.GetUtcNow()
            }));
    }

    /// <summary>
    /// Det som sker på en klocka: publiceringar vars tid passerat sedan förra gången, och påminnelser till elever
    /// som inte lämnat in en uppgift med deadline inom 24 timmar. Går att köra när som helst och hur ofta som
    /// helst, eftersom det som redan har en notis hoppas över. Returnerar antal nya notiser.
    /// </summary>
    public async Task<int> CreateScheduledAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var created = 0;

        var published = await notifications.GetPublishedBetweenAsync(now - _publishLookback, now, ct);
        foreach (var group in published.GroupBy(linked => linked.Content.Id))
        {
            created += await NotifyPublishedAsync(
                group.First().Content, group.Select(linked => linked.Link).ToList(), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        var deadlines = await notifications.GetDeadlinesBetweenAsync(now, now + _deadlineWarning, ct);
        foreach (var group in deadlines.GroupBy(linked => linked.Content.Id))
        {
            created += await RemindAboutDeadlinesAsync(group.ToList(), now, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return created;
    }

    public async Task<NotificationListDto> ListAsync(int? limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? _defaultLimit, 1, _maxLimit);
        var rows = await notifications.GetForUserAsync(currentUser.UserId, take, ct);
        return new NotificationListDto(
            rows.Select(ToDto).ToList(),
            await notifications.CountUnreadAsync(currentUser.UserId, ct));
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken ct) =>
        new(await notifications.CountUnreadAsync(currentUser.UserId, ct));

    /// <summary>Någon annans notis ger NotFound, så att id:n inte går att pröva sig fram till.</summary>
    public async Task<ServiceResult<bool>> MarkReadAsync(Guid id, CancellationToken ct)
    {
        var notification = await notifications.GetAsync(id, ct);
        if (notification is null || notification.UserId != currentUser.UserId)
        {
            return ServiceResult<bool>.NotFound("notification not found");
        }

        notification.ReadAt ??= time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    public Task<int> MarkAllReadAsync(CancellationToken ct) =>
        notifications.MarkAllReadAsync(currentUser.UserId, time.GetUtcNow(), ct);

    private async Task<int> NotifyPublishedAsync(
        Content content, IReadOnlyList<ContentLink> links, CancellationToken ct)
    {
        var kind = content.Type switch
        {
            ContentType.News => NotificationKind.NewsPublished,
            ContentType.Assignment => NotificationKind.AssignmentPublished,
            _ => (NotificationKind?)null
        };
        if (kind is null)
        {
            return 0;
        }

        var now = time.GetUtcNow();
        var recipients = new HashSet<Guid>();
        foreach (var link in links)
        {
            if (ContentVisibility.IsVisible(content, link, now) && await IsModuleOpenAsync(link, now, ct))
            {
                // Kursens lärare är de som står i kursen, inte lärarna i klassen. En länk till bara en grupp, utan
                // kurs, når gruppens lärare som förut.
                var groupIds = await GroupIdsAsync(link, ct);
                recipients.UnionWith(await notifications.GetMemberIdsAsync(groupIds, link.CourseId is null, ct));
                if (link.CourseId is not null)
                {
                    recipients.UnionWith(await notifications.GetCourseTeacherIdsAsync(link.CourseId.Value, ct));
                }
            }
        }

        recipients.Remove(content.AuthorId);
        recipients.ExceptWith(await notifications.GetNotifiedUserIdsAsync(kind.Value, content.Id, ct));
        recipients.ExceptWith(await MutedAmongAsync(recipients, kind.Value, ct));
        notifications.AddRange(recipients.Select(userId => new Notification
        {
            UserId = userId,
            Kind = kind.Value,
            ContentId = content.Id,
            CreatedAt = now
        }));
        return recipients.Count;
    }

    // Eleverna som inte lämnat in och har en deadline inom ett dygn. Har läraren begärt komplettering har eleven
    // redan fått en notis om det, så de påminns inte igen.
    private async Task<int> RemindAboutDeadlinesAsync(
        List<LinkedContent> linked, DateTimeOffset now, CancellationToken ct)
    {
        var content = linked[0].Content;
        var modes = content.Assignment?.SubmissionModes ?? [];
        if (modes.All(mode => mode == SubmissionMode.OnSite))
        {
            return 0;
        }

        var reminded = await notifications.GetNotifiedUserIdsAsync(NotificationKind.DeadlineSoon, content.Id, ct);
        var created = new List<Notification>();
        var studentsByLink = new List<(ContentLink Link, List<Guid> StudentIds)>();
        foreach (var link in linked.Select(item => item.Link))
        {
            studentsByLink.Add((link, await notifications.GetStudentIdsAsync(await GroupIdsAsync(link, ct), ct)));
        }

        var muted = await MutedAmongAsync(
            studentsByLink.SelectMany(item => item.StudentIds).ToHashSet(), NotificationKind.DeadlineSoon, ct);
        foreach (var (link, studentIds) in studentsByLink)
        {
            var linkSubmissions = (await submissions.GetByLinkAsync(link.Id, ct))
                .ToDictionary(submission => submission.StudentId);
            foreach (var studentId in studentIds)
            {
                var hasSubmission = linkSubmissions.TryGetValue(studentId, out var submission);
                if (hasSubmission && submission!.Status != SubmissionStatus.NotStarted)
                {
                    continue;
                }

                if (!muted.Contains(studentId) && reminded.Add(studentId))
                {
                    created.Add(new Notification
                    {
                        UserId = studentId,
                        Kind = NotificationKind.DeadlineSoon,
                        ContentId = content.Id,
                        SubmissionId = submission?.Id,
                        CreatedAt = now
                    });
                }
            }
        }

        notifications.AddRange(created);
        return created.Count;
    }

    // En länk till en grupp gäller gruppen (även med kurs). En länk bara till en kurs gäller alla grupper som läser den.
    private async Task<List<Guid>> GroupIdsAsync(ContentLink link, CancellationToken ct) =>
        link.GroupId is not null
            ? [link.GroupId.Value]
            : await groups.GetGroupIdsReadingCourseAsync(link.CourseId!.Value, ct);

    private async Task<bool> IsMutedAsync(Guid userId, NotificationKind kind, CancellationToken ct) =>
        (await MutedAmongAsync([userId], kind, ct)).Contains(userId);

    // Vilka av användarna som stängt av det här slaget, i en fråga.
    private async Task<HashSet<Guid>> MutedAmongAsync(
        IReadOnlyCollection<Guid> userIds, NotificationKind kind, CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var muted = await users.GetMutedKindsAsync(userIds, ct);
        return muted.Where(entry => entry.Value.Contains(kind)).Select(entry => entry.Key).ToHashSet();
    }

    private async Task<bool> IsModuleOpenAsync(ContentLink link, DateTimeOffset now, CancellationToken ct)
    {
        if (link.ModuleId is null)
        {
            return true;
        }

        var module = await modules.GetAsync(link.ModuleId.Value, ct);
        return module is null || ContentVisibility.IsModuleAvailable(module.PublishAt, now);
    }

    private static NotificationDto ToDto(NotificationRow row) =>
        new(
            row.Notification.Id,
            row.Notification.Kind,
            row.Notification.ContentId,
            row.Notification.SubmissionId,
            row.LinkId,
            row.Title,
            row.CourseId,
            row.CourseTitle,
            row.GroupName,
            row.Level,
            row.Notification.CreatedAt,
            row.Notification.ReadAt);
}
