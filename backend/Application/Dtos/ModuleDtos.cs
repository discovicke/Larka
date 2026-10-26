using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

// --- requests ---

public record CreateModuleDto(string Title, string? Description = null);

// Alla kursens moduler i den nya ordningen, var och en exakt en gång.
public record ReorderModulesDto(List<Guid> ModuleIds);

// Alla modulens delar i den nya ordningen, var och en exakt en gång.
public record ReorderContentDto(List<Guid> LinkIds);

// --- responses ---

public record ModuleDto(Guid Id, Guid CourseId, string Title, string Description, int Position)
{
    public static ModuleDto From(Module module) =>
        new(module.Id, module.CourseId, module.Title, module.Description, module.Position);
}

/// <summary>En modul med innehållet som ligger i den, i den ordning det publiceras.</summary>
public record ModuleWithContentDto(
    Guid Id,
    Guid CourseId,
    string Title,
    string Description,
    int Position,
    List<ModuleContentItem> Content)
{
    public static ModuleWithContentDto From(Module module, List<ModuleContentItem> content) =>
        new(module.Id, module.CourseId, module.Title, module.Description, module.Position, content);
}

/// <summary>
/// En del i en modul. Tar med utkast och det som publiceras senare, så att läraren ser allt (Status, PublishAt).
/// </summary>
public record ModuleContentItem(
    Guid LinkId,
    Guid ContentId,
    ContentType Type,
    GradeScale? GradeScale, // bara för uppgifter: None = övning, annars inlämning
    string Title,
    ContentStatus Status,
    Guid? GroupId,
    DateTimeOffset PublishAt,
    DateTimeOffset? DueAt,
    bool IsPinned);
