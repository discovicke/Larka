using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Abstractions;

/// <summary>
/// En notis med det inkorgen visar: innehållets titel, utdelningen den gäller, kursen eller gruppen och
/// betyget. Titel och utdelning är null om innehållet har tagits bort.
/// </summary>
public record NotificationRow(
    Notification Notification,
    string? Title,
    Guid? LinkId,
    Guid? CourseId,
    string? CourseTitle,
    string? GroupName,
    GradeLevel? Level,
    DateTimeOffset? PersonalDueAt = null);

public interface INotificationRepository
{
    void AddRange(IEnumerable<Notification> notifications);

    /// <summary>Alla medlemmar i grupperna, oavsett roll. Utan includeTeachers lämnas lärarna i klassen utanför.</summary>
    Task<List<Guid>> GetMemberIdsAsync(IReadOnlyCollection<Guid> groupIds, bool includeTeachers, CancellationToken ct);

    /// <summary>Lärarna i kursen.</summary>
    Task<List<Guid>> GetCourseTeacherIdsAsync(Guid courseId, CancellationToken ct);

    /// <summary>Eleverna i grupperna.</summary>
    Task<List<Guid>> GetStudentIdsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct);

    /// <summary>Användarna som redan har en notis av den här sorten om innehållet.</summary>
    Task<HashSet<Guid>> GetNotifiedUserIdsAsync(NotificationKind kind, Guid contentId, CancellationToken ct);

    /// <summary>Påminnelserna (DeadlineSoon) om innehållet: vem som fått en och för vilken deadline.</summary>
    Task<HashSet<(Guid UserId, DateTimeOffset DueAt)>> GetDeadlineRemindersAsync(Guid contentId, CancellationToken ct);

    /// <summary>Länkarna till innehållet.</summary>
    Task<List<ContentLink>> GetLinksAsync(Guid contentId, CancellationToken ct);

    /// <summary>
    /// Nyheter och uppgifter som blivit synliga för eleverna i intervallet (since, now]: innehållet är
    /// publicerat, länken och veckan har passerat sin publicering.
    /// </summary>
    Task<List<LinkedContent>> GetPublishedBetweenAsync(DateTimeOffset since, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Synliga uppgifter med en deadline i intervallet (now, until]: huvuddeadlinen, en kompletteringsdeadline
    /// eller en elevs personliga deadline.
    /// </summary>
    Task<List<LinkedContent>> GetDeadlinesBetweenAsync(DateTimeOffset now, DateTimeOffset until, CancellationToken ct);

    /// <summary>Användarens notiser, de nyaste först.</summary>
    Task<List<NotificationRow>> GetForUserAsync(Guid userId, int take, CancellationToken ct);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct);
    Task<Notification?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Markerar användarens olästa notiser som lästa. Returnerar hur många.</summary>
    Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset readAt, CancellationToken ct);
}
