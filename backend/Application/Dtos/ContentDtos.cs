using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

// --- requests ---

public record CreateContentDto(
    ContentType Type,
    string Title,
    string BodyMd,
    ContentStatus? Status = null,
    AssignmentKind? Kind = null, // uppgiftsfälten gäller bara uppgifter, och Kind krävs där
    GradeScale? GradeScale = null, // utan värde = ingen betygsättning
    List<SubmissionMode>? SubmissionModes = null, // ett eller flera sätt att lämna in
    string? TemplateRepo = null); // "org/mall", krävs för Repo och bara där

// Minst en av GroupId och CourseId. Båda = kursen så som den går för just den gruppen.
// ModuleId lägger innehållet i en av kursens moduler och kräver CourseId.
public record CreateContentLinkDto(
    Guid? GroupId = null,
    Guid? CourseId = null,
    Guid? ModuleId = null,
    DateTimeOffset? PublishAt = null,
    DateTimeOffset? DueAt = null,
    bool IsPinned = false);

public record CreateCommentDto(string BodyMd);

// --- responses ---

public record ContentDto(
    Guid Id,
    Guid AuthorId,
    ContentType Type,
    string Title,
    string BodyMd,
    ContentStatus Status,
    DateTimeOffset CreatedAt,
    AssignmentSpecDto? Assignment)
{
    public static ContentDto From(Content content)
    {
        var assignment = content.Assignment is null
            ? null
            : AssignmentSpecDto.From(content.Assignment);
        return new ContentDto(
            content.Id, content.AuthorId, content.Type, content.Title, content.BodyMd, content.Status,
            content.CreatedAt, assignment);
    }
}

public record AssignmentSpecDto(
    AssignmentKind Kind,
    GradeScale GradeScale,
    List<SubmissionMode> SubmissionModes,
    string? TemplateRepo)
{
    public static AssignmentSpecDto From(AssignmentSpec spec) =>
        new(spec.Kind, spec.GradeScale, spec.SubmissionModes, spec.TemplateRepo);
}

public record ContentLinkDto(
    Guid Id,
    Guid ContentId,
    Guid? GroupId,
    Guid? CourseId,
    Guid? ModuleId,
    DateTimeOffset PublishAt,
    DateTimeOffset? DueAt,
    bool IsPinned)
{
    public static ContentLinkDto From(ContentLink link) =>
        new(link.Id, link.ContentId, link.GroupId, link.CourseId, link.ModuleId, link.PublishAt, link.DueAt,
            link.IsPinned);
}

/// <summary>En rad i flödet för en grupp eller kurs: innehållet plus hur det publicerades.</summary>
public record ContentFeedItem(
    Guid LinkId,
    Guid ContentId,
    ContentType Type,
    string Title,
    string BodyMd,
    Guid AuthorId,
    Guid? GroupId,
    Guid? CourseId,
    Guid? ModuleId,
    DateTimeOffset PublishAt,
    DateTimeOffset? DueAt,
    bool IsPinned);

public record CommentDto(Guid Id, Guid ContentId, Guid AuthorId, string BodyMd, DateTimeOffset CreatedAt)
{
    public static CommentDto From(Comment comment) =>
        new(comment.Id, comment.ContentId, comment.AuthorId, comment.BodyMd, comment.CreatedAt);
}

/// <summary>Url är en tidsbegränsad länk som skapas när bilagan läses, inte något som sparas.</summary>
public record AttachmentDto(Guid Id, Guid ContentId, string FileName, string Url, long SizeBytes)
{
    public static AttachmentDto From(Attachment attachment, string url) =>
        new(attachment.Id, attachment.ContentId, attachment.FileName, url, attachment.SizeBytes);
}
