namespace Backend.Application.Dtos;

/// <summary>
/// Adminens Idag-sida: skolans kurser per läge och det som saknar något. Varje rad pekar på klassen eller kursen
/// där admin ordnar det.
/// </summary>
public record AdminOverviewDto(
    AdminCourseCountsDto Courses,
    List<AdminCourseWithoutTeacherDto> CoursesWithoutTeacher,
    List<AdminGroupRefDto> ClassesWithoutMentor,
    List<AdminUserWithoutPasswordDto> UsersWithoutPassword);

/// <summary>
/// Antal kurser per läge, räknat på datumen som i klienten. Kurser utan datum ingår i Undated.
/// </summary>
public record AdminCourseCountsDto(int Upcoming, int Running, int Finished, int Undated);

/// <summary>En kurs utan lärare. GroupId är en klass som läser kursen, där admin lägger till läraren.</summary>
public record AdminCourseWithoutTeacherDto(Guid CourseId, string Code, string Title, Guid? GroupId);

/// <summary>En klass.</summary>
public record AdminGroupRefDto(Guid GroupId, string Name, string Term);

/// <summary>En användare som inte kan logga in. GroupId är en klass hen går i, om hen går i någon.</summary>
public record AdminUserWithoutPasswordDto(Guid UserId, string Name, Guid? GroupId, string? GroupName);
