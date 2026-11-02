using Backend.Application.Dtos;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Abstractions;

/// <summary>En utdelning och innehållet den delar ut (med AssignmentSpec om det är en uppgift).</summary>
public record LinkedContent(ContentLink Link, Content Content);

/// <summary>En länk i kalendern med innehållet, kursens titel och veckans publiceringsdatum.</summary>
public record CalendarLink(
    ContentLink Link, Content Content, string? CourseTitle, DateTimeOffset? ModulePublishAt);

/// <summary>En uppgift med deadline i en kurs.</summary>
public record CourseDeadline(
    Guid LinkId, string Title, Guid CourseId, string CourseTitle, DateTimeOffset DueAt);

/// <summary>En del som publiceras i en kurs.</summary>
public record ScheduledContentLink(
    Guid LinkId, string Title, Guid CourseId, string CourseTitle, DateTimeOffset PublishAt);

/// <summary>En vecka som publiceras i en kurs.</summary>
public record ScheduledCourseModule(
    Guid ModuleId, string Title, Guid CourseId, string CourseTitle, DateTimeOffset PublishAt);

/// <summary>En referens från en utdelning till en annan del.</summary>
public record RelatedLinkRow(Guid SourceLinkId, RelatedLinkDto Link);

public interface IContentRepository
{
    Task<Content?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Lärare på någon av innehållets länkar: lärare i länkens kurs, eller i länkens grupp när länken saknar kurs.
    /// </summary>
    Task<bool> IsTeacherOfContentAsync(Guid contentId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Elev eller mentor på någon av innehållets länkar, och länken är synlig för eleverna: innehållet är publicerat,
    /// länkens PublishAt har passerat och veckan (om länken ligger i en) har öppnat. Samma datumregler som flödena
    /// och elevvyn. Länken räknas bara för grupper som läser kursen, och en länk till en viss grupp bara för den gruppen.
    /// </summary>
    Task<bool> IsVisibleToMemberOfContentAsync(
        Guid contentId, Guid userId, DateTimeOffset now, CancellationToken ct);
    void Add(Content content);
    void AddLink(ContentLink link);
    Task<LinkedContent?> GetLinkWithContentAsync(Guid linkId, CancellationToken ct);
    Task<LinkedContent?> GetLinkWithContentAsync(Guid contentId, Guid linkId, CancellationToken ct);

    /// <summary>Nästa position i modulen, eller 1 om den är tom.</summary>
    Task<int> GetNextLinkPositionAsync(Guid moduleId, CancellationToken ct);

    /// <summary>Modulens länkar i ordning, så att positionerna kan kompakteras.</summary>
    Task<List<ContentLink>> GetLinksForModuleAsync(Guid moduleId, CancellationToken ct);

    /// <summary>Modulens delar med innehåll (och AssignmentSpec), i Position-ordning.</summary>
    Task<List<LinkedContent>> GetLinksWithContentForModuleAsync(Guid moduleId, CancellationToken ct);

    /// <summary>
    /// Delarna i kursens moduler som en grupp ser: gruppens egna länkar och kursens gemensamma.
    /// Används av elevvyn, som bara visar innehåll som ligger i en vecka.
    /// </summary>
    Task<List<LinkedContent>> GetLinksWithContentForGroupCourseAsync(
        Guid groupId, Guid courseId, CancellationToken ct);

    /// <summary>Alla länkar som pekar på kursen (gemensamma och gruppens egna), med innehåll.</summary>
    Task<List<LinkedContent>> GetLinksWithContentForCourseAsync(Guid courseId, CancellationToken ct);

    /// <summary>Alla beroenden mellan delar i modulen.</summary>
    Task<List<ContentLinkDependency>> GetDependenciesForModuleAsync(Guid moduleId, CancellationToken ct);

    /// <summary>Alla beroenden mellan delar i kursens moduler.</summary>
    Task<List<ContentLinkDependency>> GetDependenciesForCourseAsync(Guid courseId, CancellationToken ct);

    /// <summary>
    /// Användarens länkar med deadline eller publicering i intervallet [from, to): gruppens egna länkar och
    /// kursens gemensamma. Bara delar och uppgifter (ModuleId eller DueAt), med kursens titel och veckans
    /// publiceringsdatum.
    /// </summary>
    Task<List<CalendarLink>> GetCalendarLinksAsync(
        Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

    /// <summary>Id:n på kurserna som har minst en publicerad (eller schemalagd) del i en vecka.</summary>
    Task<HashSet<Guid>> GetCourseIdsWithPublishedPartsAsync(
        IReadOnlyCollection<Guid> courseIds, CancellationToken ct);

    /// <summary>
    /// Sant om någon av användarens kurser eller grupper har en publicerad repo-uppgift.
    /// Används för att bara visa GitHub-CTA:n när GitHub faktiskt används i användarens undervisning.
    /// </summary>
    Task<bool> AnyRepoAssignmentAsync(
        IReadOnlyCollection<Guid> courseIds,
        IReadOnlyCollection<Guid> groupIds,
        DateTimeOffset now,
        CancellationToken ct);

    /// <summary>Projektgrupperna för en utdelning, med medlemmar och eventuellt grupprepo.</summary>
    Task<List<AssignmentGroup>> GetGroupsForLinkAsync(Guid linkId, CancellationToken ct);

    /// <summary>Publicerade uppgifter med deadline i intervallet [fromAt, toAt] i kurserna.</summary>
    Task<List<CourseDeadline>> GetCourseDeadlinesAsync(
        IReadOnlyCollection<Guid> courseIds, DateTimeOffset fromAt, DateTimeOffset toAt, CancellationToken ct);

    /// <summary>Publicerade delar som publiceras i intervallet (fromAt, toAt] i kurserna.</summary>
    Task<List<ScheduledContentLink>> GetScheduledLinksAsync(
        IReadOnlyCollection<Guid> courseIds, DateTimeOffset fromAt, DateTimeOffset toAt, CancellationToken ct);

    /// <summary>Veckor som publiceras i intervallet (fromAt, toAt] i kurserna.</summary>
    Task<List<ScheduledCourseModule>> GetScheduledModulesAsync(
        IReadOnlyCollection<Guid> courseIds, DateTimeOffset fromAt, DateTimeOffset toAt, CancellationToken ct);

    void AddDependency(ContentLinkDependency dependency);
    void RemoveDependencies(IEnumerable<ContentLinkDependency> dependencies);

    Task<List<AssignmentReference>> GetReferencesForSourceAsync(Guid sourceLinkId, CancellationToken ct);
    Task<List<RelatedLinkRow>> GetRelatedLinksForSourcesAsync(
        IReadOnlyCollection<Guid> sourceLinkIds, CancellationToken ct);
    void AddReferences(IEnumerable<AssignmentReference> references);
    void RemoveReferences(IEnumerable<AssignmentReference> references);

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
    Task<List<Attachment>> GetAttachmentsAsync(IReadOnlyCollection<Guid> contentIds, CancellationToken ct);
    Task<Attachment?> GetAttachmentAsync(Guid contentId, Guid attachmentId, CancellationToken ct);
    void RemoveAttachment(Attachment attachment);
}
