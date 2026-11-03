using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

/// <summary>
/// En notis i inkorgen. Title är innehållets titel (null om innehållet är borttaget). CourseTitle eller
/// GroupName säger var det hände. Level finns på betygsnotiser och PersonalDueAt på notiser om en personlig deadline.
/// </summary>
public record NotificationDto(
    Guid Id,
    NotificationKind Kind,
    Guid? ContentId,
    Guid? SubmissionId,
    Guid? LinkId,
    string? Title,
    Guid? CourseId,
    string? CourseTitle,
    string? GroupName,
    GradeLevel? Level,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    DateTimeOffset? PersonalDueAt = null);

/// <summary>De senaste notiserna, och antalet olästa i alla (inte bara de som skickas med).</summary>
public record NotificationListDto(List<NotificationDto> Items, int UnreadCount);

public record UnreadCountDto(int Count);
