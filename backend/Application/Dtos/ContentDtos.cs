using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

public record CreateContentDto(
    string Type,
    string Title,
    string BodyMd,
    string? Status = null,
    int? MaxScore = null,
    string? SubmissionMode = null, // bara för uppgifter: none|file|repo|fileOrRepo
    string? TemplateRepo = null); // "org/mall", krävs för repo och fileOrRepo

// Minst en av GroupId och CourseId. Båda = kursen så som den går för just den gruppen.
public record CreateContentLinkDto(
    Guid? GroupId,
    Guid? CourseId,
    DateTimeOffset? PublishAt = null,
    DateTimeOffset? DueAt = null,
    bool IsPinned = false);

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
    DateTimeOffset PublishAt,
    DateTimeOffset? DueAt,
    bool IsPinned);

public record CommentDto(string BodyMd);

public record AttachmentDto(string FileName, string Url, long SizeBytes);
