using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

public record GroupDto(Guid Id, Guid SchoolId, GroupType Type, string Name, string Term)
{
    public static GroupDto From(Group group) =>
        new(group.Id, group.SchoolId, group.Type, group.Name, group.Term);
}

public record CourseDto(
    Guid Id,
    Guid SchoolId,
    string Title,
    string Code,
    string Description,
    int? Points,
    DateOnly? StartsOn,
    DateOnly? EndsOn)
{
    public static CourseDto From(Course course) =>
        new(
            course.Id,
            course.SchoolId,
            course.Title,
            course.Code,
            course.Description,
            course.Points,
            course.StartsOn,
            course.EndsOn);
}

/// <summary>En lärare i kursen, med namnen på grupperna som läser den där hen är lärare.</summary>
public record CourseStaffDto(Guid UserId, string Name, string Email, List<string> Groups);

// Kursomgången ligger i samma skola som källkursen. Klassen är den som ska läsa den.
public record CreateCourseRunDto(string? Code, Guid GroupId, DateOnly? StartsOn, DateOnly? EndsOn, bool CopyContent);

/// <summary>Den nya kursomgången: kursens id, klassen och hur många veckor, delar och lärare som följde med.</summary>
public record CourseRunResultDto(Guid CourseId, Guid GroupId, int ModuleCount, int PartCount, int StaffCount);
