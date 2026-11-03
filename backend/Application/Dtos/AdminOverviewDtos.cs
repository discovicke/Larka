namespace Backend.Application.Dtos;

/// <summary>
/// Adminens Idag-sida: klassernas pågående kurser och det som saknar något. Varje rad pekar på klassen
/// eller kursen där admin ordnar det.
/// </summary>
public record AdminOverviewDto(
    List<AdminClassTimelineDto> ClassTimelines,
    List<AdminCourseWithoutTeacherDto> CoursesWithoutTeacher,
    List<AdminUserWithoutPasswordDto> UsersWithoutPassword,
    List<AdminUserRefDto> StudentsWithoutClass);

/// <summary>En klass med kurserna som pågår just nu.</summary>
public record AdminClassTimelineDto(Guid GroupId, string Name, List<AdminClassCourseTimelineDto> CurrentCourses);

/// <summary>En pågående kurs med datumen som tidslinjen ritar. Datumen kan saknas på gamla kurser.</summary>
public record AdminClassCourseTimelineDto(
    Guid CourseId,
    string Title,
    string Code,
    DateOnly? StartsOn,
    DateOnly? EndsOn);

/// <summary>En kurs utan lärare. GroupId är en klass som läser kursen, där admin lägger till läraren.</summary>
public record AdminCourseWithoutTeacherDto(Guid CourseId, string Code, string Title, Guid? GroupId);

/// <summary>
/// En användare som inte kan logga in: inget lösenord, och skolan har inte Microsoft-inloggning. GroupId är en klass
/// hen går i, om hen går i någon.
/// </summary>
public record AdminUserWithoutPasswordDto(Guid UserId, string Name, Guid? GroupId, string? GroupName);

/// <summary>En elev utan klass, till exempel någon som loggade in med Microsoft första gången.</summary>
public record AdminUserRefDto(Guid UserId, string Name);
