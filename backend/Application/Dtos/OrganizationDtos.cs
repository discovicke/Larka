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

public record CreateSchoolDto(string Name);

public record CreateUserDto(Guid SchoolId, string Name, string Email, UserRole Role);

public record CreateGroupDto(Guid SchoolId, GroupType Type, string Name, string? Term = null);

public record CreateCourseDto(Guid SchoolId, string Title, string Code);

public record AddMemberDto(Guid UserId, GroupRole Role = GroupRole.Student);
