namespace Backend.Application.Dtos;

/// <summary>
/// Adminens Idag-sida: skolans kurser per läge och det som saknar något. Varje rad pekar på klassen eller kursen
/// där admin ordnar det.
/// </summary>
public record AdminOverviewDto(
    AdminCourseCountsDto Courses,
    List<AdminCourseWithoutTeacherDto> CoursesWithoutTeacher,
    List<AdminUserWithoutPasswordDto> UsersWithoutPassword,
    List<AdminUserRefDto> StudentsWithoutClass);

/// <summary>
/// Antal kurser per läge, räknat på datumen som i klienten. Kurser utan datum ingår i Undated.
/// </summary>
public record AdminCourseCountsDto(int Upcoming, int Running, int Finished, int Undated);

/// <summary>En kurs utan lärare. GroupId är en klass som läser kursen, där admin lägger till läraren.</summary>
public record AdminCourseWithoutTeacherDto(Guid CourseId, string Code, string Title, Guid? GroupId);

/// <summary>
/// En användare som inte kan logga in: inget lösenord, och skolan har inte Microsoft-inloggning. GroupId är en klass
/// hen går i, om hen går i någon.
/// </summary>
public record AdminUserWithoutPasswordDto(Guid UserId, string Name, Guid? GroupId, string? GroupName);

/// <summary>En elev utan klass, till exempel någon som loggade in med Microsoft första gången.</summary>
public record AdminUserRefDto(Guid UserId, string Name);
