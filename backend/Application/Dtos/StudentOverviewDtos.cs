namespace Backend.Application.Dtos;

/// <summary>
/// En elev i klassen sett av mentorn: läget i varje kurs klassen läser. Bara läsning.
/// </summary>
public record StudentOverviewDto(Guid StudentId, string Name, Guid GroupId, List<StudentOverviewCourseDto> Courses);

/// <summary>
/// Läget i en kurs. NeedsWork är uppgifter läraren vill ha kompletterade, Late uppgifter som passerat deadline
/// utan att lämnas in, Submitted lämnade och ännu inte bedömda, och Graded de som har betyg (fördelade på nivå).
/// </summary>
public record StudentOverviewCourseDto(
    Guid CourseId,
    string Title,
    string Code,
    int AssignmentCount,
    int NeedsWorkCount,
    int LateCount,
    int SubmittedCount,
    int GradedCount,
    int FailCount,
    int PassCount,
    int DistinctionCount);
