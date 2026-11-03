using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

/// <summary>Den inloggade användaren, skolan och rollen per kurs.</summary>
public record MeDto(
    Guid Id,
    string Name,
    string Email,
    UserRole Role,
    SchoolRefDto School,
    List<CourseRoleDto> Courses,
    List<GroupRefDto> Groups,
    string? GitHubLogin = null,
    bool GitHubEnabled = false,
    bool GitHubInUse = false);

public record SchoolRefDto(Guid Id, string Name);

/// <summary>En grupp användaren är medlem i, med vilken roll som helst.</summary>
public record GroupRefDto(Guid Id, string Name);

/// <summary>Slag av notiser användaren har stängt av i appen.</summary>
public record NotificationSettingsDto(List<NotificationKind> Muted);

/// <summary>Det användaren själv får ändra om sig. Namnet kommer från Microsoft och kan rättas här.</summary>
public record UpdateProfileDto(string Name);

/// <summary>Användarens starkaste roll i kursen: Teacher eller Student.</summary>
public record CourseRoleDto(Guid CourseId, GroupRole Role);
