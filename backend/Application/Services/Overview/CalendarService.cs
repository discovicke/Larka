using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Kalendern: deadlines och publiceringstider för användarens grupper och de kurser grupperna läser.
// Eleven ser bara det som är synligt i elevvyn. Läraren ser också när publicerade delar blir synliga.
public class CalendarService(
    IContentRepository contents,
    ICurrentUser currentUser,
    TimeProvider time)
{
    private static readonly TimeSpan _maxRange = TimeSpan.FromDays(62);

    public async Task<ValidationResult<List<CalendarItemDto>>> GetAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (to <= from)
        {
            return ValidationResult<List<CalendarItemDto>>.Invalid("to must be after from");
        }

        if (to - from > _maxRange)
        {
            return ValidationResult<List<CalendarItemDto>>.Invalid("the range can be at most 62 days");
        }

        var now = time.GetUtcNow();
        var links = await contents.GetCalendarLinksAsync(currentUser.UserId, from, to, ct);
        var items = new List<CalendarItemDto>();
        foreach (var link in links)
        {
            var isTeacher = link.IsTeacher;
            var isVisible = ContentVisibility.IsVisible(link.Content, link.Link, now)
                && ContentVisibility.IsModuleAvailable(link.ModulePublishAt, now);
            if (link.Link.DueAt is not null
                && link.Link.DueAt >= from
                && link.Link.DueAt < to
                && (isTeacher || isVisible))
            {
                items.Add(ToItem(link, CalendarItemKind.Deadline, link.Link.DueAt.Value));
            }

            // Kompletteringsdeadlines gäller hela klassen, den egna deadlinen bara eleven som fått den.
            if (link.Link.DueAt is not null && (isTeacher || isVisible))
            {
                items.AddRange(link.Link.ResubmitDueAt
                    .Where(due => due >= from && due < to)
                    .Select(due => ToItem(link, CalendarItemKind.ResubmitDeadline, due)));
            }

            if (link.PersonalDueAt is not null
                && link.PersonalDueAt >= from
                && link.PersonalDueAt < to
                && isVisible)
            {
                items.Add(ToItem(link, CalendarItemKind.PersonalDeadline, link.PersonalDueAt.Value));
            }

            // Läraren ser när eleverna får se delen: när både länken och veckan har öppnat. Ett utkast publiceras
            // inte av sig självt, så det får ingen händelse.
            var publishAt = EffectivePublishAt(link);
            if (isTeacher
                && link.Content.Status == ContentStatus.Published
                && publishAt >= from
                && publishAt < to)
            {
                items.Add(ToItem(link, CalendarItemKind.Publish, publishAt));
            }
        }

        return ValidationResult<List<CalendarItemDto>>.Ok(
            items
                .OrderBy(item => item.At)
                .ThenBy(item => item.Title)
                .ToList());
    }

    private static DateTimeOffset EffectivePublishAt(CalendarLink link) =>
        link.ModulePublishAt is not null && link.ModulePublishAt > link.Link.PublishAt
            ? link.ModulePublishAt.Value
            : link.Link.PublishAt;

    private static CalendarItemDto ToItem(
        CalendarLink link, CalendarItemKind kind, DateTimeOffset at) =>
        new(
            link.Link.Id,
            link.Link.ContentId,
            link.Content.Title,
            link.Content.Type,
            link.Link.CourseId,
            link.CourseTitle,
            link.Link.GroupId,
            kind,
            at);
}
