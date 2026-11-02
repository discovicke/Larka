using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Delarnas plats i kurser och grupper (ContentLink): placera, flytta, ändra datum, sortera och ta bort.
public class ContentLinkService(
    IContentRepository contents,
    ISubmissionRepository submissionRepository,
    IPartCompletionRepository completions,
    IGroupRepository groups,
    IUserRepository users,
    IModuleRepository modules,
    SubmissionService submissions,
    NotificationService notifications,
    IPermissions permissions,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    ILogger<ContentLinkService> logger)
{
    public async Task<ServiceResult<ContentLinkDto>> AddLinkAsync(
        Guid contentId, CreateContentLinkDto dto, CancellationToken ct)
    {
        var content = await contents.GetAsync(contentId, ct);
        if (content is null)
        {
            return ServiceResult<ContentLinkDto>.NotFound("content not found");
        }

        var allowed = await CheckAsync(content, dto, ct);
        if (allowed.Status != ResultStatus.Ok)
        {
            return new ServiceResult<ContentLinkDto>(allowed.Status, null, allowed.Error);
        }

        var link = await AddAsync(content, dto, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentLinkDto>.Ok(ContentLinkDto.From(link));
    }

    /// <summary>
    /// Flyttar en del till en annan vecka i samma kurs. Länken behåller id och deadline.
    /// Beroenden tas bort eftersom de bara gäller inom en vecka.
    /// </summary>
    public async Task<ServiceResult<MoveContentLinkResultDto>> MoveLinkAsync(
        Guid contentId, Guid linkId, MoveContentLinkDto dto, CancellationToken ct)
    {
        var linked = await contents.GetLinkWithContentAsync(contentId, linkId, ct);
        if (linked is null)
        {
            return ServiceResult<MoveContentLinkResultDto>.NotFound("link not found");
        }

        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<MoveContentLinkResultDto>.Forbidden();
        }

        var link = linked.Link;
        if (link.ModuleId is null || link.CourseId is null)
        {
            return ServiceResult<MoveContentLinkResultDto>.Invalid("link is not in a week");
        }

        var sourceModule = await modules.GetAsync(link.ModuleId.Value, ct);
        var targetModule = await modules.GetAsync(dto.ModuleId, ct);
        if (sourceModule is null || targetModule is null)
        {
            return ServiceResult<MoveContentLinkResultDto>.Invalid("module not found");
        }

        if (targetModule.CourseId != link.CourseId)
        {
            return ServiceResult<MoveContentLinkResultDto>.Invalid("module does not belong to the course");
        }

        if (targetModule.Id == sourceModule.Id)
        {
            return ServiceResult<MoveContentLinkResultDto>.Invalid("link is already in that module");
        }

        var linkSubmissions = await submissionRepository.GetByLinkAsync(link.Id, ct);
        if (await StudentWorkPolicy.LinkHasStudentWorkAsync(linkSubmissions, completions, link.Id, ct))
        {
            return ServiceResult<MoveContentLinkResultDto>.Conflict(
                "the part has student activity and cannot be moved");
        }

        var dependencies = await contents.GetDependenciesForCourseAsync(link.CourseId.Value, ct);
        var affected = dependencies
            .Where(dependency => dependency.BlockedLinkId == link.Id || dependency.BlockingLinkId == link.Id)
            .ToList();
        var affectedLinkIds = affected
            .SelectMany(dependency => new[] { dependency.BlockedLinkId, dependency.BlockingLinkId })
            .Where(affectedId => affectedId != link.Id)
            .Distinct()
            .ToList();
        contents.RemoveDependencies(affected);

        var sourceLinks = await contents.GetLinksForModuleAsync(sourceModule.Id, ct);
        var remaining = sourceLinks
            .Where(sourceLink => sourceLink.Id != link.Id)
            .OrderBy(sourceLink => sourceLink.Position)
            .ToList();
        PositionOrder.Renumber(remaining);

        link.ModuleId = targetModule.Id;
        link.Position = await contents.GetNextLinkPositionAsync(targetModule.Id, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ServiceResult<MoveContentLinkResultDto>.Ok(
            new MoveContentLinkResultDto(link.Id, targetModule.Id, link.Position, affectedLinkIds));
    }

    /// <summary>Uppdaterar publicering och deadline på en länk. Flytt mellan veckor görs separat.</summary>
    public async Task<ServiceResult<ContentLinkDto>> UpdateLinkAsync(
        Guid contentId, Guid linkId, UpdateContentLinkDto dto, CancellationToken ct)
    {
        var linked = await contents.GetLinkWithContentAsync(contentId, linkId, ct);
        if (linked is null)
        {
            return ServiceResult<ContentLinkDto>.NotFound("link not found");
        }

        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<ContentLinkDto>.Forbidden();
        }

        if (dto.DueAt is not null && linked.Content.Type != ContentType.Assignment)
        {
            return ServiceResult<ContentLinkDto>.Invalid("dueAt is only allowed on assignments");
        }

        var previousPublishAt = linked.Link.PublishAt;
        linked.Link.PublishAt = dto.PublishAt ?? time.GetUtcNow();
        linked.Link.DueAt = dto.DueAt;
        linked.Link.IsPinned = dto.IsPinned;

        // Bara en ändrad publiceringstid ger notiser. Annars skulle en nål på en gammal nyhet notifiera alla.
        if (linked.Link.PublishAt != previousPublishAt)
        {
            await notifications.NotifyLinkPublishedAsync(linked.Content, linked.Link, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ContentLinkDto>.Ok(ContentLinkDto.From(linked.Link));
    }

    /// <summary>Sätter position 1, 2, 3 … i den ordning läraren skickade.</summary>
    public async Task<ServiceResult<bool>> ReorderModuleContentAsync(
        Guid courseId, Guid moduleId, ReorderContentDto dto, CancellationToken ct)
    {
        if (!await groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<bool>.NotFound("course not found");
        }

        if (!await permissions.CanTeachCourseAsync(courseId, ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        var module = await modules.GetAsync(moduleId, ct);
        if (module is null || module.CourseId != courseId)
        {
            return ServiceResult<bool>.NotFound("module not found");
        }

        if (dto.LinkIds is null)
        {
            return ServiceResult<bool>.Invalid("linkIds is required");
        }

        var links = await contents.GetLinksForModuleAsync(moduleId, ct);
        var byId = links.ToDictionary(link => link.Id);
        var listsEveryLinkOnce = dto.LinkIds.Count == links.Count
            && dto.LinkIds.Distinct().Count() == links.Count
            && dto.LinkIds.All(byId.ContainsKey);
        if (!listsEveryLinkOnce)
        {
            return ServiceResult<bool>.Invalid("linkIds must list every part in the module once");
        }

        PositionOrder.Renumber(dto.LinkIds.Select(linkId => byId[linkId]));

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// Tar bort länken. En publicerad inlämning och innehåll där eleven redan arbetat skyddas.
    /// Tomma inlämningsrader som skapades vid utdelningen tas bort med länken.
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteLinkAsync(
        Guid contentId, Guid linkId, CancellationToken ct)
    {
        var linked = await contents.GetLinkWithContentAsync(contentId, linkId, ct);
        if (linked is null)
        {
            return ServiceResult<bool>.NotFound("link not found");
        }

        if (!await permissions.CanTeachLinkAsync(linkId, ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        if (StudentWorkPolicy.IsPublishedGradedAssignment(linked.Content))
        {
            return ServiceResult<bool>.Conflict("unpublish the assignment before deleting it");
        }

        var linkSubmissions = await submissionRepository.GetByLinkAsync(linkId, ct);
        if (await StudentWorkPolicy.LinkHasStudentWorkAsync(linkSubmissions, completions, linkId, ct))
        {
            return ServiceResult<bool>.Conflict(
                "the part has student activity, files or a repository and cannot be deleted");
        }

        var deleteContent = await contents.CountLinksAsync(contentId, ct) <= 1;
        List<Attachment> attachments = deleteContent
            ? await contents.GetAttachmentsAsync(contentId, ct)
            : [];

        if (linked.Link.ModuleId is not null)
        {
            var moduleLinks = await contents.GetLinksForModuleAsync(linked.Link.ModuleId.Value, ct);
            var remaining = moduleLinks
                .Where(link => link.Id != linkId)
                .OrderBy(link => link.Position)
                .ToList();
            PositionOrder.Renumber(remaining);
        }

        foreach (var submission in linkSubmissions)
        {
            submissionRepository.Remove(submission);
        }

        contents.RemoveLink(linked.Link);
        if (deleteContent)
        {
            contents.RemoveContent(linked.Content);
        }

        await unitOfWork.SaveChangesAsync(ct);
        foreach (var attachment in attachments)
        {
            await storage.DeleteQuietlyAsync(attachment.StorageKey, logger);
        }

        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>
    /// Invalid om länken pekar fel, Forbidden om användaren inte får placera den. En länk till en kurs kräver
    /// lärare på kursen, en länk till bara en grupp lärare i gruppen.
    /// </summary>
    public async Task<ServiceResult<bool>> CheckAsync(Content content, CreateContentLinkDto dto, CancellationToken ct)
    {
        var linkError = await CheckLinkAsync(content, dto, ct);
        if (linkError is not null)
        {
            return ServiceResult<bool>.Invalid(linkError);
        }

        return await MayLinkAsync(dto, ct)
            ? ServiceResult<bool>.Ok(true)
            : ServiceResult<bool>.Forbidden();
    }

    private async Task<string?> CheckLinkAsync(
        Content content, CreateContentLinkDto dto, CancellationToken ct)
    {
        if (dto.GroupId is null && dto.CourseId is null)
        {
            return "groupId or courseId is required";
        }

        if (dto.GroupId is not null && !await groups.ExistsAsync(dto.GroupId.Value, ct))
        {
            return "group does not exist";
        }

        if (dto.CourseId is not null && !await groups.CourseExistsAsync(dto.CourseId.Value, ct))
        {
            return "course does not exist";
        }

        var linksGroupAndCourse = dto.GroupId is not null && dto.CourseId is not null;
        if (linksGroupAndCourse && !await groups.ReadsCourseAsync(dto.GroupId!.Value, dto.CourseId!.Value, ct))
        {
            return "group does not read this course";
        }

        var moduleError = await CheckModuleAsync(dto.ModuleId, dto.CourseId, ct);
        if (moduleError is not null)
        {
            return moduleError;
        }

        if (dto.DueAt is not null && content.Type != ContentType.Assignment)
        {
            return "dueAt is only allowed on assignments";
        }

        return await CheckGroupsAsync(content, dto, ct);
    }

    // Grupprepon kräver en komplett gruppindelning av länkens elevunderlag. Individuella uppgifter
    // får inte skicka med grupper.
    private async Task<string?> CheckGroupsAsync(
        Content content, CreateContentLinkDto dto, CancellationToken ct)
    {
        var workMode = content.Assignment?.WorkMode ?? RepoWorkMode.Individual;
        if (workMode != RepoWorkMode.Group)
        {
            return dto.Groups is not null && dto.Groups.Count > 0
                ? "groups are only allowed on group repo assignments"
                : null;
        }

        if (dto.Groups is null || dto.Groups.Count == 0)
        {
            return "groups are required on group repo assignments";
        }

        var names = dto.Groups.Select(group => group.Name.Trim()).ToList();
        if (names.Any(string.IsNullOrWhiteSpace))
        {
            return "every group needs a name";
        }

        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
        {
            return "group names must be unique";
        }

        if (dto.Groups.Any(group => group.StudentIds is null || group.StudentIds.Count == 0))
        {
            return "every group needs at least one student";
        }

        var studentIds = dto.Groups.SelectMany(group => group.StudentIds).ToList();
        if (studentIds.Distinct().Count() != studentIds.Count)
        {
            return "a student can only belong to one group";
        }

        var groupIds = dto.GroupId is not null
            ? [dto.GroupId.Value]
            : await groups.GetGroupIdsReadingCourseAsync(dto.CourseId!.Value, ct);
        var eligible = (await users.GetStudentsInGroupsAsync(groupIds, ct))
            .Select(student => student.Id)
            .ToHashSet();
        if (studentIds.Any(studentId => !eligible.Contains(studentId)))
        {
            return "a group contains a student who does not read the course";
        }

        return eligible.Except(studentIds).Any()
            ? "every student in the course must belong to a group"
            : null;
    }

    /// <summary>Lägger till länken och delar ut uppgiften. Sparar inte.</summary>
    public async Task<ContentLink> AddAsync(
        Content content, CreateContentLinkDto dto, CancellationToken ct)
    {
        var position = dto.ModuleId is null
            ? 0
            : await contents.GetNextLinkPositionAsync(dto.ModuleId.Value, ct);

        var link = new ContentLink
        {
            ContentId = content.Id,
            GroupId = dto.GroupId,
            CourseId = dto.CourseId,
            ModuleId = dto.ModuleId,
            Position = position,
            PublishAt = dto.PublishAt ?? time.GetUtcNow(),
            DueAt = dto.DueAt,
            IsPinned = dto.IsPinned
        };
        if (dto.Groups is not null && dto.Groups.Count > 0)
        {
            var groupPosition = 0;
            link.Groups = dto.Groups
                .Select(group => new AssignmentGroup
                {
                    ContentLinkId = link.Id,
                    Name = group.Name.Trim(),
                    Position = ++groupPosition,
                    Members = group.StudentIds
                        .Select(studentId => new AssignmentGroupMember
                        {
                            AssignmentGroupId = Guid.Empty,
                            StudentId = studentId
                        })
                        .ToList()
                })
                .ToList();
            foreach (var group in link.Groups)
            {
                foreach (var member in group.Members)
                {
                    member.AssignmentGroupId = group.Id;
                }
            }
        }

        contents.AddLink(link);
        await submissions.HandOutAsync(link, content, ct);
        await notifications.NotifyLinkPublishedAsync(content, link, ct);
        return link;
    }

    // En länk till en kurs kräver lärare på kursen, en länk till bara en grupp lärare i gruppen.
    // CheckLinkAsync har redan kontrollerat att minst en av dem finns.
    private async Task<bool> MayLinkAsync(CreateContentLinkDto dto, CancellationToken ct) =>
        dto.CourseId is not null
            ? await permissions.CanTeachCourseAsync(dto.CourseId.Value, ct)
            : await permissions.CanTeachGroupAsync(dto.GroupId!.Value, ct);

    /// <summary>Returnerar felet, eller null om modulen saknas eller hör till kursen.</summary>
    private async Task<string?> CheckModuleAsync(Guid? moduleId, Guid? courseId, CancellationToken ct)
    {
        if (moduleId is null)
        {
            return null;
        }

        if (courseId is null)
        {
            return "moduleId requires courseId";
        }

        var module = await modules.GetAsync(moduleId.Value, ct);
        if (module is null)
        {
            return "module does not exist";
        }

        return module.CourseId == courseId
            ? null
            : "module does not belong to the course";
    }
}
