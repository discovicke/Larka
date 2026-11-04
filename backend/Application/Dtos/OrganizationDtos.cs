using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

public record SchoolDto(Guid Id, string Name)
{
    public static SchoolDto From(School school) => new(school.Id, school.Name);
}

/// <summary>
/// DeactivatedAt är satt när admin har spärrat användaren. MicrosoftLinked är sant när hen har loggat in med
/// Microsoft.
/// </summary>
public record UserDto(
    Guid Id,
    Guid SchoolId,
    string Name,
    string Email,
    UserRole Role,
    DateTimeOffset? DeactivatedAt,
    bool MicrosoftLinked)
{
    public static UserDto From(User user) =>
        new(user.Id, user.SchoolId, user.Name, user.Email, user.Role, user.DeactivatedAt, user.ExternalId is not null);
}

public record MemberDto(Guid UserId, Guid GroupId, GroupRole Role)
{
    public static MemberDto From(Enrollment enrollment) =>
        new(enrollment.UserId, enrollment.GroupId, enrollment.Role);
}

/// <summary>En medlem i en grupp, med namn och rollen i just den gruppen.</summary>
public record GroupMemberDto(Guid UserId, string Name, GroupRole Role, string? AvatarUrl = null);

public record CreateSchoolDto(string Name);

/// <summary>
/// Microsoft-inloggning för skolan. Configured: appen har Auth:Entra. Linked: skolan tar emot inloggningar från
/// TenantId, appens tenant.
/// </summary>
public record SchoolMicrosoftDto(bool Configured, bool Linked, string? TenantId);

/// <summary>Password är valfritt. Utan lösenord kan användaren inte logga in med e-post och lösenord.</summary>
public record CreateUserDto(Guid SchoolId, string Name, string Email, UserRole Role, string? Password = null);

public record UpdateUserRoleDto(UserRole Role);

public record CreateGroupDto(Guid SchoolId, GroupType Type, string Name);

/// <summary>Ändrar gruppens namn och typ. Skolan går inte att byta.</summary>
public record UpdateGroupDto(GroupType Type, string Name);

/// <summary>Poäng och datum är valfria och kan ändras senare med PUT.</summary>
public record CreateCourseDto(
    Guid SchoolId,
    string Title,
    string Code,
    int? Points = null,
    DateOnly? StartsOn = null,
    DateOnly? EndsOn = null);

/// <summary>Ändrar kursens uppgifter. Koden är låst, eftersom den är unik per skola.</summary>
public record UpdateCourseDto(
    string Title,
    string Description,
    int? Points,
    DateOnly? StartsOn,
    DateOnly? EndsOn);

public record AddMemberDto(Guid UserId, GroupRole Role = GroupRole.Student);

public record UpdateMemberDto(GroupRole Role);
