using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

/// <summary>En rad i kalendern: en deadline eller en publiceringstid för en del.</summary>
public record CalendarItemDto(
    Guid LinkId,
    Guid ContentId,
    string Title,
    ContentType ContentType,
    Guid? CourseId,
    string? CourseTitle,
    Guid? GroupId,
    CalendarItemKind Kind,
    DateTimeOffset At);
