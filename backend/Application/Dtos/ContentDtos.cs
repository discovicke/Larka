using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

// --- requests ---

public record CreateContentDto(
    ContentType Type,
    string Title,
    string BodyMd,
    ContentStatus? Status = null,
    GradeScale? GradeScale = null, // uppgiftsfälten gäller bara uppgifter. Utan skala = övning, med = inlämning
    List<SubmissionMode>? SubmissionModes = null, // ett eller flera sätt att lämna in
    string? TemplateRepo = null, // "org/mall", krävs för Repo och bara där
    string? RepoNamePattern = null, // t.ex. "lab2-{githubLogin}"; null = standardmönstret
    RepoWorkMode? WorkMode = null,
    bool? LockAtDeadline = null,
    bool? FeedbackPullRequest = null, // fas 2
    bool? AllowLate = null, // fas 2
    List<AssignmentRequirementInput>? Requirements = null);

public record AssignmentRequirementInput(
    string Title,
    string? TestPattern = null,
    bool IsManual = false);

// Skapar innehållet och länken i samma transaktion, så att ett fel i länken inte lämnar ett orphan.
public record CreateContentWithLinkDto(CreateContentDto Content, CreateContentLinkDto Link);

// Minst en av GroupId och CourseId. Båda = kursen så som den går för just den gruppen.
// ModuleId lägger innehållet i en av kursens moduler och kräver CourseId.
public record CreateContentLinkDto(
    Guid? GroupId = null,
    Guid? CourseId = null,
    Guid? ModuleId = null,
    DateTimeOffset? PublishAt = null,
    DateTimeOffset? DueAt = null,
    bool IsPinned = false,
    List<AssignmentGroupInput>? Groups = null,
    List<Guid>? RelatedLinkIds = null,
    List<DateTimeOffset>? ResubmitDueAt = null);

/// <summary>Projektgrupp vid skapandet av en gruppuppgift. Alla elev-id:n måste finnas i kursen.</summary>
public record AssignmentGroupInput(string Name, List<Guid> StudentIds, Guid? Id = null);

public record CreateCommentDto(string BodyMd);

// Typen får inte ändras efter skapandet, så den finns inte med här. Status ändras via /status.
// Fälten för uppgifter skickas som null för teori och som tomma värden för en övning.
public record UpdateContentDto(
    string Title,
    string BodyMd,
    GradeScale? GradeScale = null,
    List<SubmissionMode>? SubmissionModes = null,
    string? TemplateRepo = null,
    string? RepoNamePattern = null,
    RepoWorkMode? WorkMode = null,
    bool? LockAtDeadline = null,
    bool? FeedbackPullRequest = null, // fas 2
    bool? AllowLate = null, // fas 2
    List<AssignmentRequirementInput>? Requirements = null);

public record UpdateContentStatusDto(ContentStatus Status);

/// <summary>Förhandsvisning av reponamn. Värdena är exempel som läraren ser i formuläret.</summary>
/// <summary>En publicerad del som en uppgift hänvisar till.</summary>
public record RelatedLinkDto(
    Guid LinkId,
    Guid ContentId,
    string Title,
    ContentType Type,
    Guid? ModuleId,
    string? ModuleTitle,
    Guid? CourseId);

public record RepoNamePreviewRequest(
    string? Pattern,
    string AssignmentTitle,
    string? GitHubLogin = null,
    string? StudentName = null,
    string? StudentEmail = null,
    Guid? StudentId = null,
    RepoWorkMode? WorkMode = null,
    string? GroupName = null,
    int? GroupNumber = null);

public record RepoNamePreviewDto(string Name, string Pattern);

// ModuleId flyttas i ett eget flöde, eftersom flytten också påverkar ordningen och beroenden.
public record UpdateContentLinkDto(
    DateTimeOffset? PublishAt = null,
    DateTimeOffset? DueAt = null,
    bool IsPinned = false,
    List<Guid>? RelatedLinkIds = null,
    List<DateTimeOffset>? ResubmitDueAt = null);

public record MoveContentLinkDto(Guid ModuleId);

// --- responses ---

public record CreatedContentWithLinkDto(ContentDto Content, ContentLinkDto Link);

// AffectedLinkIds är de delar som förlorade ett beroende när delen flyttades.
public record MoveContentLinkResultDto(
    Guid LinkId,
    Guid ModuleId,
    int Position,
    List<Guid> AffectedLinkIds);

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
    GradeScale GradeScale,
    List<SubmissionMode> SubmissionModes,
    string? TemplateRepo,
    string? RepoNamePattern,
    RepoWorkMode WorkMode,
    bool LockAtDeadline,
    bool FeedbackPullRequest,
    bool AllowLate,
    List<AssignmentRequirementDto> Requirements)
{
    public static AssignmentSpecDto From(AssignmentSpec spec) =>
        new(
            spec.GradeScale,
            spec.SubmissionModes,
            spec.TemplateRepo,
            spec.RepoNamePattern,
            spec.WorkMode,
            spec.LockAtDeadline,
            spec.FeedbackPullRequest,
            spec.AllowLate,
            spec.Requirements
                .OrderBy(requirement => requirement.Position)
                .Select(AssignmentRequirementDto.From)
                .ToList());
}

public record AssignmentRequirementDto(
    Guid Id,
    string Title,
    string? TestPattern,
    bool IsManual,
    int Position)
{
    public static AssignmentRequirementDto From(AssignmentRequirement requirement) =>
        new(
            requirement.Id,
            requirement.Title,
            requirement.TestPattern,
            requirement.IsManual,
            requirement.Position);
}

public record ContentLinkDto(
    Guid Id,
    Guid ContentId,
    Guid? GroupId,
    Guid? CourseId,
    Guid? ModuleId,
    DateTimeOffset PublishAt,
    DateTimeOffset? DueAt,
    bool IsPinned,
    List<DateTimeOffset> ResubmitDueAt)
{
    public static ContentLinkDto From(ContentLink link) =>
        new(link.Id, link.ContentId, link.GroupId, link.CourseId, link.ModuleId, link.PublishAt, link.DueAt,
            link.IsPinned, link.ResubmitDueAt);
}

/// <summary>En rad i flödet för en grupp eller kurs: innehållet plus hur det publicerades.</summary>
public record ContentFeedItem(
    Guid LinkId,
    Guid ContentId,
    ContentType Type,
    string Title,
    string BodyMd,
    Guid AuthorId,
    string AuthorName,
    Guid? GroupId,
    Guid? CourseId,
    Guid? ModuleId,
    DateTimeOffset PublishAt,
    DateTimeOffset? DueAt,
    bool IsPinned);

public record CommentDto(
    Guid Id,
    Guid ContentId,
    Guid AuthorId,
    string AuthorName,
    string BodyMd,
    DateTimeOffset CreatedAt)
{
    public static CommentDto From(Comment comment, string authorName) =>
        new(comment.Id, comment.ContentId, comment.AuthorId, authorName, comment.BodyMd, comment.CreatedAt);
}

/// <summary>Url är en tidsbegränsad länk som skapas när bilagan läses, inte något som sparas.</summary>
public record AttachmentDto(Guid Id, Guid ContentId, string FileName, string Url, long SizeBytes)
{
    public static AttachmentDto From(Attachment attachment, string url) =>
        new(attachment.Id, attachment.ContentId, attachment.FileName, url, attachment.SizeBytes);
}

/// <summary>Uppdaterar en gruppindelning efter att uppgiften delats ut. Bara befintliga grupper får ändras.</summary>
public record UpdateAssignmentGroupsDto(List<AssignmentGroupInput> Groups);
