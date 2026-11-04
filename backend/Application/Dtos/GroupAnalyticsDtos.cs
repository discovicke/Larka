namespace Backend.Application.Dtos;

/// <summary>
/// Läget i en klass för utbildningsledaren: klassens kurser och varje elevs bedömda uppgifter.
/// Bara uppgifter som eleven kan lämna in räknas, och bara publicerat innehåll i publicerade veckor.
/// </summary>
public record GroupAnalyticsDto(
    Guid GroupId,
    string GroupName,
    List<GroupAnalyticsCourseDto> Courses,
    List<GroupAnalyticsStudentDto> Students);

/// <summary>En kurs i klassen, med antal uppgifter och hur bedömningarna faller ut.</summary>
public record GroupAnalyticsCourseDto(
    Guid CourseId,
    string Title,
    string Code,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    int StudentCount,
    int AssignmentCount,
    int GradedCount,
    int FailCount,
    int PassCount,
    int DistinctionCount,
    int NeedsWorkCount,
    int SubmittedCount,
    int OverdueCount,
    int AutoFailCount,
    int AtRiskStudentCount);

/// <summary>En elev och elevens läge i varje kurs. NeedsSupport är sant när något behöver följas upp.</summary>
public record GroupAnalyticsStudentDto(
    Guid StudentId,
    string Name,
    bool NeedsSupport,
    List<GroupAnalyticsStudentCourseDto> Courses);

/// <summary>En elevs räknare i en kurs. Overdue är uppgifter som passerat deadline utan inlämning medan en kompletteringsdeadline återstår.
/// AutoFail är uppgifter där sista deadline passerat utan inlämning eller komplettering, alltså automatiskt IG.
/// De räknas också som bedömda (GradedCount) och som IG (FailCount), så AutoFail är en delmängd av båda.</summary>
public record GroupAnalyticsStudentCourseDto(
    Guid CourseId,
    int AssignmentCount,
    int GradedCount,
    int FailCount,
    int PassCount,
    int DistinctionCount,
    int NeedsWorkCount,
    int SubmittedCount,
    int OverdueCount,
    int AutoFailCount,
    bool NeedsSupport);
