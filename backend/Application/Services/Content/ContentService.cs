using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Content skapas fristående och publiceras sedan med en eller flera länkar (AddLinkAsync).
public class ContentService(
    IContentRepository contents,
    IGroupRepository groups,
    NotificationService notifications,
    IPermissions permissions,
    ContentAccess access,
    ContentLinkService links,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time)
{
    // Ett utkast syns bara för dem som får ändra det, och för admin i skolan som läser det.
    public async Task<ServiceResult<ContentDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var content = await contents.GetAsync(id, ct);
        return content is null || !await access.MayReadAsync(content, ct)
            ? ServiceResult<ContentDto>.NotFound("content not found")
            : ServiceResult<ContentDto>.Ok(ContentDto.From(content));
    }

    /// <summary>
    /// Visar läraren samma reponamn som provisioning skapar. Exempelvärden används när eleven inte är vald.
    /// </summary>
    public async Task<ServiceResult<RepoNamePreviewDto>> PreviewRepoNameAsync(
        RepoNamePreviewRequest request, CancellationToken ct)
    {
        if (!await permissions.CanCreateContentAsync(ct))
        {
            return ServiceResult<RepoNamePreviewDto>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(request.AssignmentTitle))
        {
            return ServiceResult<RepoNamePreviewDto>.Invalid("assignmentTitle is required");
        }

        var workMode = request.WorkMode ?? RepoWorkMode.Individual;
        var patternError = GitHubRepoNaming.Validate(request.Pattern, workMode);
        if (patternError is not null)
        {
            return ServiceResult<RepoNamePreviewDto>.Invalid(patternError);
        }

        var values = new RepoNameValues(
            request.AssignmentTitle,
            request.GitHubLogin,
            string.IsNullOrWhiteSpace(request.StudentName)
                ? "Elev"
                : request.StudentName,
            string.IsNullOrWhiteSpace(request.StudentEmail)
                ? "elev@example.com"
                : request.StudentEmail,
            request.StudentId ?? Guid.Empty,
            string.IsNullOrWhiteSpace(request.GroupName)
                ? "Grupp 1"
                : request.GroupName,
            request.GroupNumber ?? 1);
        var effectivePattern = workMode == RepoWorkMode.Group
            && string.IsNullOrWhiteSpace(request.Pattern)
                ? GitHubRepoNaming.DefaultGroupPattern
                : request.Pattern;
        var pattern = GitHubRepoNaming.EffectivePattern(effectivePattern);
        return ServiceResult<RepoNamePreviewDto>.Ok(
            new RepoNamePreviewDto(GitHubRepoNaming.Render(effectivePattern, values), pattern));
    }

    // Typ, status och läge är enums i DTO:n, så okända värden stoppas redan när JSON läses in (400).
    public async Task<ServiceResult<ContentDto>> CreateAsync(CreateContentDto dto, CancellationToken ct)
    {
        if (!await permissions.CanCreateContentAsync(ct))
        {
            return ServiceResult<ContentDto>.Forbidden();
        }

        var built = BuildContent(dto);
        if (built.Error is not null)
        {
            return ServiceResult<ContentDto>.Invalid(built.Error);
        }

        var content = built.Value!;
        contents.Add(content);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentDto>.Ok(ContentDto.From(content));
    }

    // Innehåll och länk skapas tillsammans. Ett enda SaveChanges gör att ett fel i länken inte lämnar
    // ett content utan länk, och att utdelningen av uppgifter hamnar i samma transaktion.
    public async Task<ServiceResult<CreatedContentWithLinkDto>> CreateWithLinkAsync(
        CreateContentWithLinkDto dto, CancellationToken ct)
    {
        if (!await permissions.CanCreateContentAsync(ct))
        {
            return ServiceResult<CreatedContentWithLinkDto>.Forbidden();
        }

        if (dto.Content is null || dto.Link is null)
        {
            return ServiceResult<CreatedContentWithLinkDto>.Invalid("content and link are required");
        }

        var built = BuildContent(dto.Content);
        if (built.Error is not null)
        {
            return ServiceResult<CreatedContentWithLinkDto>.Invalid(built.Error);
        }

        var content = built.Value!;
        var allowed = await links.CheckAsync(content, dto.Link, ct);
        if (allowed.Status != ResultStatus.Ok)
        {
            return new ServiceResult<CreatedContentWithLinkDto>(allowed.Status, null, allowed.Error);
        }

        contents.Add(content);
        var link = await links.AddAsync(content, dto.Link, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<CreatedContentWithLinkDto>.Ok(
            new CreatedContentWithLinkDto(ContentDto.From(content), ContentLinkDto.From(link)));
    }

    /// <summary>NotFound om innehållet inte finns, 403 om användaren inte får ändra det.</summary>
    public async Task<ServiceResult<ContentDto>> UpdateStatusAsync(
        Guid id, UpdateContentStatusDto dto, CancellationToken ct)
    {
        var content = await contents.GetAsync(id, ct);
        if (content is null)
        {
            return ServiceResult<ContentDto>.NotFound("content not found");
        }

        if (!await access.MayEditAsync(content, ct))
        {
            return ServiceResult<ContentDto>.Forbidden();
        }

        var wasPublished = content.Status == ContentStatus.Published;
        content.Status = dto.Status;
        if (content.Status == ContentStatus.Published && !wasPublished)
        {
            await notifications.NotifyContentPublishedAsync(content, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentDto>.Ok(ContentDto.From(content));
    }

    /// <summary>Uppdaterar innehållet. Typen är låst och status ändras via UpdateStatusAsync.</summary>
    public async Task<ServiceResult<ContentDto>> UpdateAsync(
        Guid id, UpdateContentDto dto, CancellationToken ct)
    {
        var content = await contents.GetAsync(id, ct);
        if (content is null)
        {
            return ServiceResult<ContentDto>.NotFound("content not found");
        }

        if (!await access.MayEditAsync(content, ct))
        {
            return ServiceResult<ContentDto>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ServiceResult<ContentDto>.Invalid("title is required");
        }

        if (content.Type == ContentType.News && string.IsNullOrWhiteSpace(dto.BodyMd))
        {
            return ServiceResult<ContentDto>.Invalid("news text is required");
        }

        var lengthError = LengthError(dto.Title, dto.BodyMd);
        if (lengthError is not null)
        {
            return ServiceResult<ContentDto>.Invalid(lengthError);
        }

        var fieldError = AssignmentFieldsValidator.Check(
            content.Type,
            dto.GradeScale,
            dto.SubmissionModes,
            dto.TemplateRepo,
            dto.RepoNamePattern,
            dto.WorkMode ?? RepoWorkMode.Individual,
            dto.FeedbackPullRequest ?? false,
            dto.AllowLate ?? false,
            dto.Requirements);
        if (fieldError is not null)
        {
            return ServiceResult<ContentDto>.Invalid(fieldError);
        }

        content.Title = dto.Title.Trim();
        content.BodyMd = dto.BodyMd;

        if (content.Type == ContentType.Assignment)
        {
            var spec = content.Assignment ?? new AssignmentSpec { ContentId = content.Id };
            spec.GradeScale = dto.GradeScale ?? GradeScale.None;
            spec.SubmissionModes = (dto.SubmissionModes ?? []).Distinct().ToList();
            spec.TemplateRepo = dto.TemplateRepo;
            ApplyGitHubSettings(
                spec,
                dto.RepoNamePattern,
                dto.WorkMode ?? RepoWorkMode.Individual,
                dto.LockAtDeadline ?? true,
                dto.FeedbackPullRequest ?? false,
                dto.AllowLate ?? false);
            ApplyRequirements(spec, dto.Requirements);
            // En regel som inte längre kan uppfyllas får inte låsa nästa del för alltid.
            if (spec.GradeScale == GradeScale.None || spec.SubmissionModes.Count == 0)
            {
                spec.CompletionRule = CompletionRule.Submitted;
            }

            content.Assignment = spec;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentDto>.Ok(ContentDto.From(content));
    }

    /// <summary>
    /// Filtrerar på typ (news|theory|assignment) om den går att tolka, annars returneras alla. Bara gruppens
    /// medlemmar, lärare i en kurs gruppen läser och admin i skolan får läsa. Utkast ingår aldrig i flödet.
    /// Admin och gruppens elever ser hela flödet. En lärare ser bara kurserna hen undervisar i, och det som är riktat
    /// till bara gruppen om hen är lärare i gruppen, aldrig gruppens övriga kurser.
    /// </summary>
    public async Task<ServiceResult<List<ContentFeedItem>>> GetGroupFeedAsync(
        Guid groupId, string? type, CancellationToken ct)
    {
        if (!await groups.ExistsAsync(groupId, ct))
        {
            return ServiceResult<List<ContentFeedItem>>.NotFound("group not found");
        }

        if (!await permissions.CanReadGroupAsync(groupId, ct))
        {
            return ServiceResult<List<ContentFeedItem>>.Forbidden();
        }

        var feed = await contents.GetGroupFeedAsync(groupId, ParseType(type), time.GetUtcNow(), ct);
        var classTeacher = await groups.IsTeacherAsync(groupId, currentUser.UserId, ct);
        if (await permissions.IsAdminAsync(ct)
            || (!classTeacher && await groups.IsMemberAsync(groupId, currentUser.UserId, ct)))
        {
            return ServiceResult<List<ContentFeedItem>>.Ok(feed);
        }

        // Lärarrollen i klassen ger bara det som är riktat till klassen utan kurs, aldrig klassens kurser.
        var taught = (await groups.GetCourseRolesAsync(currentUser.UserId, ct))
            .Where(role => role.Role == GroupRole.Teacher)
            .Select(role => role.CourseId)
            .ToHashSet();
        return ServiceResult<List<ContentFeedItem>>.Ok(feed
            .Where(item => item.CourseId is null
                ? classTeacher
                : taught.Contains(item.CourseId.Value))
            .ToList());
    }

    /// <summary>
    /// Som gruppens flöde, men för kursen: lärare och elev i kursen, och admin i skolan får läsa.
    /// </summary>
    public async Task<ServiceResult<List<ContentFeedItem>>> GetCourseFeedAsync(
        Guid courseId, string? type, CancellationToken ct)
    {
        if (!await groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<List<ContentFeedItem>>.NotFound("course not found");
        }

        if (!await permissions.CanReadCourseAsync(courseId, ct))
        {
            return ServiceResult<List<ContentFeedItem>>.Forbidden();
        }

        return ServiceResult<List<ContentFeedItem>>.Ok(
            await contents.GetCourseFeedAsync(courseId, ParseType(type), time.GetUtcNow(), ct));
    }

    private static string? LengthError(string title, string? bodyMd)
    {
        if (title.Trim().Length > TextLimits.Title)
        {
            return TextLimits.TitleTooLong;
        }

        return TextLimits.IsTooLong(bodyMd, TextLimits.ContentBody)
            ? TextLimits.ContentBodyTooLong
            : null;
    }

    private ValidationResult<Content> BuildContent(CreateContentDto dto)
    {
        var type = dto.Type;
        var status = dto.Status ?? ContentStatus.Published;

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ValidationResult<Content>.Invalid("title is required");
        }

        if (type == ContentType.News && string.IsNullOrWhiteSpace(dto.BodyMd))
        {
            return ValidationResult<Content>.Invalid("news text is required");
        }

        var lengthError = LengthError(dto.Title, dto.BodyMd);
        if (lengthError is not null)
        {
            return ValidationResult<Content>.Invalid(lengthError);
        }

        var fieldError = AssignmentFieldsValidator.Check(
            type,
            dto.GradeScale,
            dto.SubmissionModes,
            dto.TemplateRepo,
            dto.RepoNamePattern,
            dto.WorkMode ?? RepoWorkMode.Individual,
            dto.FeedbackPullRequest ?? false,
            dto.AllowLate ?? false,
            dto.Requirements);
        if (fieldError is not null)
        {
            return ValidationResult<Content>.Invalid(fieldError);
        }

        var modes = (dto.SubmissionModes ?? []).Distinct().ToList();
        var scale = dto.GradeScale ?? GradeScale.None;

        var content = new Content
        {
            AuthorId = currentUser.UserId,
            Type = type,
            Title = dto.Title.Trim(),
            BodyMd = dto.BodyMd,
            Status = status
        };
        if (type == ContentType.Assignment)
        {
            var spec = new AssignmentSpec
            {
                ContentId = content.Id,
                GradeScale = scale,
                SubmissionModes = modes,
                TemplateRepo = dto.TemplateRepo
            };
            ApplyGitHubSettings(
                spec,
                dto.RepoNamePattern,
                dto.WorkMode ?? RepoWorkMode.Individual,
                dto.LockAtDeadline ?? true,
                dto.FeedbackPullRequest ?? false,
                dto.AllowLate ?? false);
            ApplyRequirements(spec, dto.Requirements);
            content.Assignment = spec;
        }

        return ValidationResult<Content>.Ok(content);
    }

    private static void ApplyGitHubSettings(
        AssignmentSpec spec,
        string? repoNamePattern,
        RepoWorkMode workMode,
        bool lockAtDeadline,
        bool feedbackPullRequest,
        bool allowLate)
    {
        if (!spec.UsesRepo)
        {
            spec.RepoNamePattern = null;
            spec.WorkMode = RepoWorkMode.Individual;
            spec.LockAtDeadline = true;
            spec.FeedbackPullRequest = false;
            spec.AllowLate = false;
            return;
        }

        spec.RepoNamePattern = string.IsNullOrWhiteSpace(repoNamePattern)
            ? null
            : repoNamePattern.Trim();
        spec.WorkMode = workMode;
        spec.LockAtDeadline = lockAtDeadline;
        spec.FeedbackPullRequest = feedbackPullRequest;
        spec.AllowLate = allowLate;
    }

    private static void ApplyRequirements(
        AssignmentSpec spec, List<AssignmentRequirementInput>? requirements)
    {
        spec.Requirements.Clear();
        if (!spec.UsesRepo || requirements is null)
        {
            return;
        }

        for (var index = 0; index < requirements.Count; index++)
        {
            var requirement = requirements[index];
            spec.Requirements.Add(new AssignmentRequirement
            {
                ContentId = spec.ContentId,
                Title = requirement.Title.Trim(),
                TestPattern = requirement.IsManual
                    ? null
                    : requirement.TestPattern?.Trim(),
                IsManual = requirement.IsManual,
                Position = index
            });
        }
    }

    private static ContentType? ParseType(string? type) =>
        Enum.TryParse<ContentType>(type, true, out var contentType)
            ? contentType
            : null;
}
