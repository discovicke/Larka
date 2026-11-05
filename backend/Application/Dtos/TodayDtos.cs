using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

/// <summary>Elevens Idag-sida. Tomma listor när det inte finns något att visa.</summary>
public record TodayDto(
    List<TodayTodoDto> Todo,
    List<TodaySubmissionDto> Submitted,
    List<TodayFeedbackDto> Feedback,
    List<TodayNewsDto> News,
    List<TodayCourseDto> Courses);

/// <summary>En uppgift som väntar: publicerad, med deadline från nu och inte inlämnad.</summary>
public record TodayTodoDto(
    Guid LinkId,
    Guid ContentId,
    string Title,
    Guid? CourseId,
    string? CourseTitle,
    DateTimeOffset DueAt,
    SubmissionStatus Status);

/// <summary>En inlämning eleven gjort, med status och eventuellt betyg.</summary>
public record TodaySubmissionDto(
    Guid LinkId,
    string Title,
    Guid? CourseId,
    string? CourseTitle,
    DateTimeOffset? SubmittedAt,
    SubmissionStatus Status,
    GradeLevel? Grade);

/// <summary>
/// Lärarens återkoppling på en inlämning. Grade är null vid komplettering (NeedsWork) och på en övning, som bara får
/// feedback.
/// </summary>
public record TodayFeedbackDto(
    Guid LinkId,
    string Title,
    Guid? CourseId,
    string? CourseTitle,
    GradeLevel? Grade,
    bool NeedsWork,
    string Feedback,
    DateTimeOffset GradedAt);

/// <summary>En nyhet från elevens kurser och grupper. Source är kursens eller gruppens namn.</summary>
public record TodayNewsDto(
    Guid ContentId,
    string Title,
    Guid? CourseId,
    string? Source,
    DateTimeOffset PublishAt);

/// <summary>En kurs eleven läser, med hur många av delarna som är klara.</summary>
public record TodayCourseDto(
    Guid CourseId,
    string Code,
    string Title,
    int TotalParts,
    int CompletedParts,
    DateOnly? StartsOn,
    DateOnly? EndsOn);
