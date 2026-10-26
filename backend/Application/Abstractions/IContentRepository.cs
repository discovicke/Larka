using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Abstractions;

/// <summary>En utdelning och innehållet den delar ut (med AssignmentSpec om det är en uppgift).</summary>
public record LinkedContent(ContentLink Link, Content Content);

public interface IContentRepository
{
    Task<Content?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    void Add(Content content);
    void AddLink(ContentLink link);
    Task<LinkedContent?> GetLinkWithContentAsync(Guid linkId, CancellationToken ct);
    Task<LinkedContent?> GetLinkWithContentAsync(Guid contentId, Guid linkId, CancellationToken ct);

    /// <summary>Nästa position i modulen, eller 1 om den är tom.</summary>
    Task<int> GetNextLinkPositionAsync(Guid moduleId, CancellationToken ct);

    /// <summary>Modulens länkar i ordning, så att positionerna kan kompakteras.</summary>
    Task<List<ContentLink>> GetLinksForModuleAsync(Guid moduleId, CancellationToken ct);

    Task<int> CountLinksAsync(Guid contentId, CancellationToken ct);
    void RemoveLink(ContentLink link);
    void RemoveContent(Content content);

    // Publicerat innehåll med PublishAt passerad. Gruppens flöde tar även med det som är länkat
    // till en kurs gruppen läser (länkar med bara CourseId).
    Task<List<ContentFeedItem>> GetGroupFeedAsync(Guid groupId, ContentType? type, DateTimeOffset now,
        CancellationToken ct);

    Task<List<ContentFeedItem>> GetCourseFeedAsync(Guid courseId, ContentType? type, DateTimeOffset now,
        CancellationToken ct);

    Task<List<Comment>> GetCommentsAsync(Guid contentId, CancellationToken ct);
    void AddComment(Comment comment);
    void AddAttachment(Attachment attachment);
    Task<List<Attachment>> GetAttachmentsAsync(Guid contentId, CancellationToken ct);
    Task<Attachment?> GetAttachmentAsync(Guid contentId, Guid attachmentId, CancellationToken ct);
    void RemoveAttachment(Attachment attachment);
}
