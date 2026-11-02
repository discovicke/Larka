using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

// --- requests ---

public record CreateModuleDto(string Title, string? Description = null, DateTimeOffset? PublishAt = null);

public record UpdateModuleDto(string Title, string? Description = null, DateTimeOffset? PublishAt = null);

// Alla kursens moduler i den nya ordningen, var och en exakt en gång.
public record ReorderModulesDto(List<Guid> ModuleIds);

// Alla modulens delar i den nya ordningen, var och en exakt en gång.
public record ReorderContentDto(List<Guid> LinkIds);

// Veckans roadmap. Varje del i veckan ska finnas med exakt en gång.
// CompletionRule styr när en uppgift med inlämningssätt räknas som klar, och får bara sättas på dem.
public record SaveRoadmapDto(List<RoadmapLinkDto> Links);

public record RoadmapLinkDto(
    Guid LinkId,
    List<Guid> BlockedByLinkIds,
    CompletionRule? CompletionRule = null);

// Veckorna i källkursen som kopieras till slutet av målkursen, i källkursens ordning.
public record CopyWeeksDto(Guid SourceCourseId, List<Guid> ModuleIds);

// --- responses ---

/// <summary>Veckorna som skapades i målkursen, i ordning, och hur många delar de innehåller.</summary>
public record CopyWeeksResultDto(List<ModuleDto> Modules, int PartCount);

// Veckorna vars utkast ska publiceras. Utan lista gäller det alla kursens veckor.
public record PublishWeeksDto(List<Guid>? ModuleIds = null);

public record PublishWeeksResultDto(int WeekCount, int PartCount);


public record ModuleDto(
    Guid Id,
    Guid CourseId,
    string Title,
    string Description,
    int Position,
    DateTimeOffset? PublishAt)
{
    public static ModuleDto From(Module module) =>
        new(module.Id, module.CourseId, module.Title, module.Description, module.Position, module.PublishAt);
}

/// <summary>En modul med innehållet som ligger i den, i den ordning det publiceras.</summary>
public record ModuleWithContentDto(
    Guid Id,
    Guid CourseId,
    string Title,
    string Description,
    int Position,
    DateTimeOffset? PublishAt,
    List<ModuleContentItem> Content)
{
    public static ModuleWithContentDto From(Module module, List<ModuleContentItem> content) =>
        new(module.Id, module.CourseId, module.Title, module.Description, module.Position, module.PublishAt, content);
}

/// <summary>
/// En del i en modul. Tar med utkast och det som publiceras senare, så att läraren ser allt (Status, PublishAt).
/// BlockedBy är de delar i samma vecka som måste vara klara först.
/// SubmittedCount/StudentCount räknar unika elever: den som lämnat in flera gånger räknas en gång, och nämnaren
/// är eleverna i länkens grupp, eller i alla grupper som läser kursen när länken gäller hela kursen.
/// </summary>
public record ModuleContentItem(
    Guid LinkId,
    Guid ContentId,
    ContentType Type,
    GradeScale? GradeScale, // bara för uppgifter: None = övning, annars inlämning
    CompletionRule? CompletionRule, // bara för uppgifter med inlämningssätt
    string Title,
    ContentStatus Status,
    Guid? GroupId,
    DateTimeOffset PublishAt,
    DateTimeOffset? DueAt,
    bool IsPinned,
    List<Guid> BlockedBy,
    bool HasStudentActivity,
    int? SubmittedCount = null, // elever som har lämnat in, bara för läraren och bara för delar med inlämningssätt
    int? StudentCount = null); // elever i de grupper som delen delas ut till
