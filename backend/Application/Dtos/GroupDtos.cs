using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

/// <summary>
/// Gruppen med antal medlemmar (alla roller), så att listan slipper hämta medlemmarna en och en.
/// LastCourseEndsOn är den sista slutdagen bland gruppens kurser. Den är null när gruppen saknar kurser eller när
/// någon kurs saknar slutdatum, så att klienten kan avgöra om en klass är avslutad utan att hämta kurserna.
/// </summary>
public record GroupDto(
    Guid Id,
    Guid SchoolId,
    GroupType Type,
    string Name,
    string Term,
    int MemberCount,
    DateOnly? LastCourseEndsOn)
{
    public static GroupDto From(Group group, int memberCount, DateOnly? lastCourseEndsOn) =>
        new(group.Id, group.SchoolId, group.Type, group.Name, group.Term, memberCount, lastCourseEndsOn);
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

/// <summary>En lärare i kursen, med namnen på grupperna som läser kursen.</summary>
public record CourseStaffDto(Guid UserId, string Name, string Email, List<string> Groups);

/// <summary>En lärare i kursen, som admin ser den.</summary>
public record CourseTeacherDto(Guid UserId, string Name, string Email);

/// <summary>
/// Lärarna i en kurs som en klass läser, och namnen på de andra grupperna som läser samma kurs.
/// </summary>
public record GroupCourseTeachersDto(Guid CourseId, List<CourseTeacherDto> Teachers, List<string> SharedWith);

/// <summary>Gör användaren till lärare i kursen. Användaren ska vara lärare eller admin i kursens skola.</summary>
public record AddCourseTeacherDto(Guid UserId);

/// <summary>Elev i kursen som ännu inte har kopplat ett GitHub-konto.</summary>
public record UnlinkedGitHubStudentDto(Guid Id, string Name, string Email);

/// <summary>Elev i kursen, för gruppindelning och översikter.</summary>
public record CourseStudentDto(Guid Id, string Name, string Email, string? GitHubLogin);

// Kursomgången ligger i samma skola som källkursen. Klassen är den som ska läsa den.
public record CreateCourseRunDto(
    string? Code,
    Guid GroupId,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    bool CopyContent,
    string? Title = null,
    int? Points = null,
    List<Guid>? TeacherIds = null,
    List<Guid>? ModuleIds = null);

/// <summary>Den nya kursomgången: kursens id, klassen och hur många veckor, delar och lärare som följde med.</summary>
public record CourseRunResultDto(Guid CourseId, Guid GroupId, int ModuleCount, int PartCount, int StaffCount);
