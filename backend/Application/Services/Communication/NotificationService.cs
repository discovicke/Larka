using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Notiser till användarna: skapas när något publiceras, bedöms eller kommenteras, och läses i inkorgen.
// Metoderna som skapar notiser (Notify...) sparar inte själva. Anroparen sparar i samma transaktion som
// ändringen som gav notisen. CreateScheduledAsync, som jobbet anropar, sparar däremot själv.
// En nyhet eller uppgift ger högst en notis per användare (NewsPublished, AssignmentPublished), och en påminnelse
// (DeadlineSoon) högst en per användare och deadline, så att samma sak aldrig dyker upp två gånger.
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

    /// <summary>Notis till eleven om att läraren har satt en personlig deadline.</summary>
    public Task NotifyPersonalDeadlineSetAsync(Submission submission, Guid contentId, CancellationToken ct) =>
        NotifySubmissionAsync(submission, contentId, NotificationKind.PersonalDeadlineSet, ct);

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
    /// De som får läsa innehållet och inte är spärrade: medlemmarna där en länk syns och lärarna i länkarnas
    /// kurser, också innan innehållet syns för eleverna. Det är dem en kommentar kan nämna.
    /// </summary>
    public async Task<List<User>> GetReadersAsync(Content content, CancellationToken ct)
    {
        var links = await notifications.GetLinksAsync(content.Id, ct);
        var readers = await VisibleToAsync(content, links, time.GetUtcNow(), ct);
        foreach (var courseId in links.Where(link => link.CourseId is not null).Select(link => link.CourseId!.Value)
                     .Distinct())
        {
            readers.UnionWith(await notifications.GetCourseTeacherIdsAsync(courseId, ct));
        }

        return await users.GetActiveAsync(readers, ct);
    }

    /// <summary>
    /// Notiser om en ny eller ändrad kommentar. De som nämns i den (CommentMention) och får läsa innehållet får en
    /// notis, utom den som skrev. Vid en ändring (previousBody satt) får bara de som lagts till i ändringen en.
    /// En ny kommentar ger också innehållets författare och de som kommenterat förut (threadIds) en CommentReply,
    /// men bara om de inte redan har en oläst om samma innehåll, så att en livlig tråd blir en rad i inkorgen.
    /// </summary>
    public async Task NotifyCommentAsync(
        Content content,
        Comment comment,
        string? previousBody,
        IReadOnlyCollection<Guid> threadIds,
        CancellationToken ct)
    {
        var mentioned = Mentions.UserIdsIn(comment.BodyMd);
        var isNew = previousBody is null;
        if (mentioned.Count == 0 && !isNew)
        {
            return;
        }

        var readers = (await GetReadersAsync(content, ct)).Select(user => user.Id).ToHashSet();
        var now = time.GetUtcNow();

        var mentionRecipients = mentioned.Where(readers.Contains).ToHashSet();
        mentionRecipients.Remove(comment.AuthorId);
        mentionRecipients.ExceptWith(Mentions.UserIdsIn(previousBody));
        mentionRecipients.ExceptWith(
            await notifications.GetNotifiedAboutCommentAsync(NotificationKind.CommentMention, comment.Id, ct));
        mentionRecipients.ExceptWith(await MutedAmongAsync(mentionRecipients, NotificationKind.CommentMention, ct));
        notifications.AddRange(mentionRecipients.Select(userId =>
            CommentNotification(userId, NotificationKind.CommentMention, content, comment, now)));

        if (!isNew)
        {
            return;
        }

        var replyRecipients = threadIds.Append(content.AuthorId).Where(readers.Contains).ToHashSet();
        replyRecipients.Remove(comment.AuthorId);
        replyRecipients.ExceptWith(mentioned);
        replyRecipients.ExceptWith(
            await notifications.GetUnreadNotifiedUserIdsAsync(NotificationKind.CommentReply, content.Id, ct));
        replyRecipients.ExceptWith(await MutedAmongAsync(replyRecipients, NotificationKind.CommentReply, ct));
        notifications.AddRange(replyRecipients.Select(userId =>
            CommentNotification(userId, NotificationKind.CommentReply, content, comment, now)));
    }

    /// <summary>
    /// Det som sker på en klocka: publiceringar vars tid passerat sedan förra gången, och påminnelser till elever
    /// som har en deadline kvar att lämna in till inom 24 timmar (huvud, komplettering eller egen). Går att köra när som helst och hur ofta som
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
        var recipients = await VisibleToAsync(content, links, now, ct);
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

    // Eleverna vars nästa deadline (GradingWindow.NextDeadline: huvud, komplettering eller egen) ligger inom ett
    // dygn och som får lämna in till den: Inte inlämnad och Komplettera. En påminnelse per elev och deadline, så
    // en elev som ska komplettera påminns före kompletteringsdeadlinen även om hen redan fått en före huvuddeadlinen.
    private async Task<int> RemindAboutDeadlinesAsync(
        List<LinkedContent> linked, DateTimeOffset now, CancellationToken ct)
    {
        var content = linked[0].Content;
        var modes = content.Assignment?.SubmissionModes ?? [];
        if (modes.All(mode => mode == SubmissionMode.OnSite))
        {
            return 0;
        }

        var reminded = await notifications.GetDeadlineRemindersAsync(content.Id, ct);
        var created = new List<Notification>();
        var studentsByLink = new List<(ContentLink Link, List<Guid> StudentIds)>();
        foreach (var link in linked.Select(item => item.Link))
        {
            var studentIds = await notifications.GetMemberIdsAsync(
                await GroupIdsAsync(link, ct), includeTeachers: false, ct);
            studentsByLink.Add((link, studentIds));
        }

        var muted = await MutedAmongAsync(
            studentsByLink.SelectMany(item => item.StudentIds).ToHashSet(), NotificationKind.DeadlineSoon, ct);
        foreach (var (link, studentIds) in studentsByLink)
        {
            var linkSubmissions = (await submissions.GetByLinkAsync(link.Id, ct))
                .ToDictionary(submission => submission.StudentId);
            foreach (var studentId in studentIds.Where(studentId => !muted.Contains(studentId)))
            {
                linkSubmissions.TryGetValue(studentId, out var submission);
                var status = submission?.Status ?? SubmissionStatus.NotStarted;
                if (status is not (SubmissionStatus.NotStarted or SubmissionStatus.NeedsWork))
                {
                    continue;
                }

                var dueAt = GradingWindow.Of(link, submission, now).NextDeadline;
                if (dueAt is null || dueAt.Value > now + _deadlineWarning || !reminded.Add((studentId, dueAt.Value)))
                {
                    continue;
                }

                created.Add(new Notification
                {
                    UserId = studentId,
                    Kind = NotificationKind.DeadlineSoon,
                    ContentId = content.Id,
                    SubmissionId = submission?.Id,
                    DueAt = dueAt,
                    CreatedAt = now
                });
            }
        }

        notifications.AddRange(created);
        return created.Count;
    }

    // Medlemmarna där någon av länkarna syns för eleverna nu. Kursens lärare är de som står i kursen, inte lärarna
    // i klassen. En länk till bara en grupp, utan kurs, når gruppens lärare.
    private async Task<HashSet<Guid>> VisibleToAsync(
        Content content, IReadOnlyList<ContentLink> links, DateTimeOffset now, CancellationToken ct)
    {
        var recipients = new HashSet<Guid>();
        foreach (var link in links)
        {
            if (ContentVisibility.IsVisible(content, link, now) && await IsModuleOpenAsync(link, now, ct))
            {
                var groupIds = await GroupIdsAsync(link, ct);
                recipients.UnionWith(await notifications.GetMemberIdsAsync(groupIds, link.CourseId is null, ct));
                if (link.CourseId is not null)
                {
                    recipients.UnionWith(await notifications.GetCourseTeacherIdsAsync(link.CourseId.Value, ct));
                }
            }
        }

        return recipients;
    }

    private static Notification CommentNotification(
        Guid userId, NotificationKind kind, Content content, Comment comment, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            Kind = kind,
            ContentId = content.Id,
            CommentId = comment.Id,
            ActorId = comment.AuthorId,
            CreatedAt = now
        };

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
            row.Notification.ReadAt,
            row.PersonalDueAt,
            row.Notification.CommentId,
            row.ActorName);
}
