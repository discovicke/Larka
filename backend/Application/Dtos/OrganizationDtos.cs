using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

public record SchoolDto(Guid Id, string Name)
{
    public static SchoolDto From(School school) => new(school.Id, school.Name);
}

public record UserDto(Guid Id, Guid SchoolId, string Name, string Email, UserRole Role)
{
    public static UserDto From(User user) => new(user.Id, user.SchoolId, user.Name, user.Email, user.Role);
}

public record MemberDto(Guid UserId, Guid GroupId, GroupRole Role)
{
    public static MemberDto From(Enrollment enrollment) =>
        new(enrollment.UserId, enrollment.GroupId, enrollment.Role);
}

/// <summary>En medlem i en grupp, med namn och rollen i just den gruppen.</summary>
public record GroupMemberDto(Guid UserId, string Name, GroupRole Role);

public record CreateSchoolDto(string Name);

/// <summary>Password är valfritt. Utan lösenord kan användaren inte logga in med e-post och lösenord.</summary>
public record CreateUserDto(Guid SchoolId, string Name, string Email, UserRole Role, string? Password = null);

public record CreateGroupDto(Guid SchoolId, GroupType Type, string Name, string? Term = null);

/// <summary>Ändrar gruppens namn, typ och termin. Skolan går inte att byta.</summary>
public record UpdateGroupDto(GroupType Type, string Name, string? Term = null);

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
