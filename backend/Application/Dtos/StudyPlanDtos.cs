using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

/// <summary>Elevens studieplan: poängen hen klarat av och kurserna hen läser.</summary>
public record StudyPlanDto(
    int CompletedPoints,
    int TotalPoints,
    List<StudyPlanCourseDto> Courses);

/// <summary>
/// En kurs i studieplanen. Grade är alltid null än, eftersom modellen inte har något slutbetyg på kursen.
/// </summary>
public record StudyPlanCourseDto(
    Guid CourseId,
    string Title,
    string Code,
    int? Points,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    StudyPlanCourseStatus Status,
    GradeLevel? Grade,
    int AssignmentCount,
    int SubmittedCount,
    int GradedCount,
    List<StudyPlanAssignmentDto> Assignments);

/// <summary>En uppgift eleven kan lämna in i kursen, med elevens egen inlämning och betyg.</summary>
public record StudyPlanAssignmentDto(
    Guid LinkId,
    string Title,
    DateTimeOffset? DueAt,
    SubmissionStatus Status,
    GradeLevel? Grade,
    string? Feedback,
    DateTimeOffset? GradedAt);
