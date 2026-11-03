using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Kalendern: deadlines och publiceringstider för användarens grupper och de kurser grupperna läser.
// Eleven ser bara det som är synligt i elevvyn. Läraren ser också när delarna publiceras.
public class CalendarService(
    IContentRepository contents,
    IPermissions permissions,
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
            var isTeacher = await permissions.CanTeachLinkAsync(link.Link.Id, ct);
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

            if (isTeacher
                && link.Link.PublishAt >= from
                && link.Link.PublishAt < to)
            {
                items.Add(ToItem(link, CalendarItemKind.Publish, link.Link.PublishAt));
            }
        }

        return ValidationResult<List<CalendarItemDto>>.Ok(
            items
                .OrderBy(item => item.At)
                .ThenBy(item => item.Title)
                .ToList());
    }

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
