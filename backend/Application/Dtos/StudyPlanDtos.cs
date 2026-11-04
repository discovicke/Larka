using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

/// <summary>
/// Elevens studieplan och kurserna hen läser. CompletedPoints är poängen för avslutade kurser med godkänt kursbetyg
/// (G eller VG), och EndedPoints poängen för alla avslutade kurser, oavsett betyg. Kursbetyg finns inte i modellen
/// än, så CompletedPoints är 0 tills de gör det.
/// </summary>
public record StudyPlanDto(
    int CompletedPoints,
    int EndedPoints,
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
    List<DateTimeOffset> ResubmitDueAt,
    DateTimeOffset? PersonalDueAt,
    SubmissionStatus Status,
    GradeLevel? Grade,
    string? Feedback,
    DateTimeOffset? GradedAt,
    DeadlineStateDto? Deadline = null);
