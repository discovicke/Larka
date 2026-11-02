namespace Backend.Application.Dtos;

/// <summary>
/// Lärarens Idag-sida: inlämningar att bedöma, deadlines, schemalagda delar och kurserna.
/// Tomma listor när användaren inte undervisar i någon kurs.
/// </summary>
public record TeachingDto(
    List<TeachingToGradeDto> ToGrade,
    int ToGradeTotal,
    List<TeachingDeadlineDto> Deadlines,
    List<TeachingScheduledDto> Scheduled,
    List<TeachingCourseDto> Courses);

/// <summary>En inlämning som väntar på bedömning. Äldst först, högst åtta i listan.</summary>
public record TeachingToGradeDto(
    Guid SubmissionId,
    Guid LinkId,
    string Title,
    Guid CourseId,
    string CourseTitle,
    Guid StudentId,
    string StudentName,
    DateTimeOffset SubmittedAt);

/// <summary>En publicerad uppgift med deadline de närmaste två veckorna.</summary>
public record TeachingDeadlineDto(
    Guid LinkId,
    string Title,
    Guid CourseId,
    string CourseTitle,
    DateTimeOffset DueAt,
    int Submitted,
    int Students);

/// <summary>En del eller vecka som publiceras de närmaste sju dagarna. Kind är "Part" eller "Week".</summary>
public record TeachingScheduledDto(
    Guid? LinkId,
    Guid? ModuleId,
    string Title,
    Guid CourseId,
    string CourseTitle,
    DateTimeOffset PublishAt,
    string Kind);

/// <summary>
/// En kurs användaren undervisar i, med antal elever och inlämningar att bedöma. NeedsPreparation är sant när
/// beskrivningen är tom, eller när kursen ännu inte har börjat och saknar publicerade delar.
/// </summary>
public record TeachingCourseDto(
    Guid CourseId,
    string Code,
    string Title,
    int Students,
    int ToGrade,
    bool NeedsPreparation,
    DateOnly? StartsOn,
    DateOnly? EndsOn);
