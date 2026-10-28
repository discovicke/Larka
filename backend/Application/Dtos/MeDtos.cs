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
    string? GitHubLogin = null);

public record SchoolRefDto(Guid Id, string Name);

/// <summary>Användarens starkaste roll i kursen: Teacher, Mentor eller Student.</summary>
public record CourseRoleDto(Guid CourseId, GroupRole Role);
