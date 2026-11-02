using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

// --- requests ---

public record SetCompletionDto(bool Completed);

public record SetChecklistTickDto(bool Checked);

// --- responses ---

/// <summary>Vilka punkter i uppgiftens checklista eleven har bockat av (plats från 0).</summary>
public record ChecklistDto(List<int> CheckedIndexes);

/// <summary>En vecka som eleven ser den: bara publicerat innehåll, med lås och avklarande.</summary>
public record StudentModuleWithContentDto(
    Guid Id,
    Guid CourseId,
    string Title,
    string Description,
    int Position,
    DateTimeOffset? PublishAt,
    bool IsAvailable,
    List<StudentContentItem> Content);

/// <summary>
/// En del i veckan. BodyMd är null när delen är låst, så att texten inte går att läsa i förväg.
/// CanMarkDone är true för teori och uppgifter utan inlämningssätt; uppgifter med inlämning
/// blir klara genom Submission och har Submitted/Grade i stället.
/// </summary>
public record StudentContentItem(
    Guid LinkId,
    Guid ContentId,
    ContentType Type,
    GradeScale? GradeScale,
    string Title,
    string? BodyMd,
    DateTimeOffset PublishAt,
    DateTimeOffset? DueAt,
    List<DateTimeOffset> ResubmitDueAt,
    bool IsPinned,
    bool IsLocked,
    bool IsCompleted,
    bool CanMarkDone,
    List<StudentBlockerDto> BlockedBy,
    SubmissionStatus? SubmissionStatus,
    GradeLevel? GradeLevel,
    bool HasRepoMode,
    List<SubmissionMode> SubmissionModes,
    List<AssignmentRequirementDto> Requirements,
    StudentRepoDto? Repo,
    AssignmentGroupDto? Group = null,
    List<RelatedLinkDto>? RelatedLinks = null);

/// <summary>
/// En förutsättning som eleven saknar. Title är null när förutsättningen inte är publicerad än,
/// så att ett utkast inte läcker sin titel.
/// </summary>
public record StudentBlockerDto(Guid LinkId, string? Title, bool IsSatisfied);
