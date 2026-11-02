using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Elevens vecka: vilka delar som är tillgängliga, vilka som är låsta och vad eleven klarat.
// Klarmarkering och inlämning är per elev och del (ContentLink).
public class StudentContentService(
    IContentRepository contents,
    IModuleRepository modules,
    IGroupRepository groups,
    ISubmissionRepository submissions,
    IPartCompletionRepository completions,
    IChecklistRepository checklist,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider time)
{
    // Den inloggade användarens vy av kursen. Grupperna slås upp här, aldrig av klienten: eleven ser
    // sina egna gruppers länkar och kursens gemensamma. Läser eleven kursen i flera grupper får hen
    // unionen av grupperna. Den som inte är elev i någon grupp som läser kursen får 403.
    public async Task<ServiceResult<List<StudentModuleWithContentDto>>> GetForCurrentUserAsync(
        Guid courseId, CancellationToken ct)
    {
        if (!await groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<List<StudentModuleWithContentDto>>.NotFound("course not found");
        }

        var groupIds = await StudentGroupsReadingCourseAsync(courseId, ct);
        return groupIds.Count == 0
            ? ServiceResult<List<StudentModuleWithContentDto>>.Forbidden()
            : ServiceResult<List<StudentModuleWithContentDto>>.Ok(await BuildAsync(groupIds, courseId, ct));
    }

    public async Task<ServiceResult<StudentModuleWithContentDto>> SetCompletionAsync(
        Guid courseId, Guid linkId, SetCompletionDto dto, CancellationToken ct)
    {
        if (!await groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<StudentModuleWithContentDto>.NotFound("course not found");
        }

        var groupIds = await StudentGroupsReadingCourseAsync(courseId, ct);
        if (groupIds.Count == 0)
        {
            return ServiceResult<StudentModuleWithContentDto>.Forbidden();
        }

        var linked = await FindOpenPartAsync(courseId, linkId, groupIds, ct);
        if (linked?.Link.ModuleId is null)
        {
            return ServiceResult<StudentModuleWithContentDto>.NotFound("part not found");
        }

        if (linked.Content.Type == ContentType.News)
        {
            return ServiceResult<StudentModuleWithContentDto>.Invalid("news cannot be completed");
        }

        if (!PartRules.IsManual(linked.Content))
        {
            return ServiceResult<StudentModuleWithContentDto>.Invalid(
                "this part is completed by handing in the assignment");
        }

        var existing = await completions.GetAsync(linkId, currentUser.UserId, ct);
        if (dto.Completed && existing is null)
        {
            var missing = await MissingBlockersAsync(
                groupIds, courseId, linked.Link.ModuleId.Value, linkId, ct);
            if (missing.Count > 0)
            {
                return ServiceResult<StudentModuleWithContentDto>.Conflict(
                    $"the part is locked by: {string.Join(", ", missing)}");
            }

            completions.Add(new PartCompletion
            {
                ContentLinkId = linkId,
                StudentId = currentUser.UserId,
                CompletedAt = time.GetUtcNow()
            });
            await unitOfWork.SaveChangesAsync(ct);
        }
        else if (!dto.Completed && existing is not null)
        {
            completions.Remove(existing);
            await unitOfWork.SaveChangesAsync(ct);
        }

        var courseModules = await BuildAsync(groupIds, courseId, ct);
        return ServiceResult<StudentModuleWithContentDto>.Ok(
            courseModules.Single(module => module.Id == linked.Link.ModuleId.Value));
    }

    /// <summary>Punkterna eleven har bockat av i uppgiftens checklista.</summary>
    public async Task<ServiceResult<ChecklistDto>> GetChecklistAsync(
        Guid courseId, Guid linkId, CancellationToken ct)
    {
        var access = await CheckChecklistAccessAsync(courseId, linkId, ct);
        return access ?? ServiceResult<ChecklistDto>.Ok(await ChecklistOfAsync(linkId, ct));
    }

    /// <summary>Bockar av eller bockar ur en punkt. Upprepade anrop med samma värde ändrar inget.</summary>
    public async Task<ServiceResult<ChecklistDto>> SetChecklistTickAsync(
        Guid courseId, Guid linkId, int index, SetChecklistTickDto dto, CancellationToken ct)
    {
        if (index is < 0 or >= _maxChecklistItems)
        {
            return ServiceResult<ChecklistDto>.Invalid($"index must be between 0 and {_maxChecklistItems - 1}");
        }

        var access = await CheckChecklistAccessAsync(courseId, linkId, ct);
        if (access is not null)
        {
            return access;
        }

        var ticks = await checklist.GetForStudentAsync(linkId, currentUser.UserId, ct);
        var existing = ticks.Where(tick => tick.ItemIndex == index).ToList();
        if (dto.Checked && existing.Count == 0)
        {
            checklist.Add(new ChecklistTick
            {
                ContentLinkId = linkId,
                StudentId = currentUser.UserId,
                ItemIndex = index,
                CheckedAt = time.GetUtcNow()
            });
            await unitOfWork.SaveChangesAsync(ct);
        }
        else if (!dto.Checked && existing.Count > 0)
        {
            checklist.RemoveRange(existing);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return ServiceResult<ChecklistDto>.Ok(await ChecklistOfAsync(linkId, ct));
    }

    // Så många punkter får en checklista ha. Håller antalet rader per elev och del begränsat.
    private const int _maxChecklistItems = 100;

    private async Task<ChecklistDto> ChecklistOfAsync(Guid linkId, CancellationToken ct) =>
        new((await checklist.GetForStudentAsync(linkId, currentUser.UserId, ct))
            .Select(tick => tick.ItemIndex)
            .ToList());

    // Samma regler som för klarmarkering: eleven ska gå i en grupp som läser kursen och delen ska vara synlig.
    private async Task<ServiceResult<ChecklistDto>?> CheckChecklistAccessAsync(
        Guid courseId, Guid linkId, CancellationToken ct)
    {
        if (!await groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<ChecklistDto>.NotFound("course not found");
        }

        var groupIds = await StudentGroupsReadingCourseAsync(courseId, ct);
        if (groupIds.Count == 0)
        {
            return ServiceResult<ChecklistDto>.Forbidden();
        }

        return await FindOpenPartAsync(courseId, linkId, groupIds, ct) is null
            ? ServiceResult<ChecklistDto>.NotFound("part not found")
            : null;
    }

    // Delen om den finns i kursen, i en vecka som har öppnat, hör till elevens grupper och är publicerad.
    private async Task<LinkedContent?> FindOpenPartAsync(
        Guid courseId, Guid linkId, IReadOnlyList<Guid> groupIds, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var linked = await contents.GetLinkWithContentAsync(linkId, ct);
        var module = linked?.Link.ModuleId is null
            ? null
            : await modules.GetAsync(linked.Link.ModuleId.Value, ct);
        return linked is null
            || linked.Link.CourseId != courseId
            || linked.Link.ModuleId is null
            || module is null
            || !IsModuleAvailable(module, now)
            || (linked.Link.GroupId is not null && !groupIds.Contains(linked.Link.GroupId.Value))
            || !IsVisible(linked, now)
                ? null
                : linked;
    }

    private async Task<List<Guid>> StudentGroupsReadingCourseAsync(Guid courseId, CancellationToken ct)
    {
        var mine = await groups.GetStudentGroupIdsAsync(currentUser.UserId, ct);
        var reading = await groups.GetGroupIdsReadingCourseAsync(courseId, ct);
        return mine.Intersect(reading).Order().ToList();
    }

    // Kursens gemensamma länkar kommer med i varje grupp, så unionen tas distinkt på länken.
    private async Task<List<LinkedContent>> LinksForGroupsAsync(
        IReadOnlyList<Guid> groupIds, Guid courseId, CancellationToken ct)
    {
        var all = new Dictionary<Guid, LinkedContent>();
        foreach (var groupId in groupIds)
        {
            foreach (var link in await contents.GetLinksWithContentForGroupCourseAsync(groupId, courseId, ct))
            {
                all.TryAdd(link.Link.Id, link);
            }
        }

        return all.Values.ToList();
    }

    private async Task<List<StudentModuleWithContentDto>> BuildAsync(
        IReadOnlyList<Guid> groupIds, Guid courseId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var links = await LinksForGroupsAsync(groupIds, courseId, ct);
        var courseModules = await modules.GetForCourseAsync(courseId, ct);
        var dependencies = await contents.GetDependenciesForCourseAsync(courseId, ct);
        var linkIds = links.Select(link => link.Link.Id).ToList();
        var studentCompletions = await completions.GetForStudentAsync(currentUser.UserId, linkIds, ct);
        var studentSubmissions = await submissions.GetForStudentAsync(currentUser.UserId, linkIds, ct);

        var byLinkId = links.ToDictionary(link => link.Link.Id);
        var completionByLinkId = studentCompletions.ToDictionary(completion => completion.ContentLinkId);
        var submissionByLinkId = studentSubmissions.ToDictionary(submission => submission.ContentLinkId);
        var blockersByLinkId = dependencies.ToLookup(dependency => dependency.BlockedLinkId);
        var availableModuleIds = courseModules
            .Where(module => IsModuleAvailable(module, now))
            .Select(module => module.Id)
            .ToHashSet();

        var itemsByModuleId = links
            .Where(link => availableModuleIds.Contains(link.Link.ModuleId!.Value) && IsVisible(link, now))
            .GroupBy(link => link.Link.ModuleId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(link => ToItem(
                        link, now, blockersByLinkId, byLinkId, completionByLinkId, submissionByLinkId))
                    .ToList());

        return courseModules
            .Select(module => new StudentModuleWithContentDto(
                module.Id,
                module.CourseId,
                module.Title,
                module.Description,
                module.Position,
                module.PublishAt,
                IsModuleAvailable(module, now),
                itemsByModuleId.GetValueOrDefault(module.Id) ?? []))
            .ToList();
    }

    private static StudentContentItem ToItem(
        LinkedContent link,
        DateTimeOffset now,
        ILookup<Guid, ContentLinkDependency> blockersByLinkId,
        Dictionary<Guid, LinkedContent> byLinkId,
        Dictionary<Guid, PartCompletion> completionByLinkId,
        Dictionary<Guid, Submission> submissionByLinkId)
    {
        var blockers = blockersByLinkId[link.Link.Id]
            .Select(blocker => new StudentBlockerDto(
                blocker.BlockingLinkId,
                TitleOfBlocker(blocker.BlockingLinkId, now, byLinkId),
                IsSatisfied(
                    blocker.BlockingLinkId,
                    byLinkId,
                    completionByLinkId,
                    submissionByLinkId)))
            .ToList();
        var isLocked = blockers.Any(blocker => !blocker.IsSatisfied);
        var submission = submissionByLinkId.GetValueOrDefault(link.Link.Id);
        var repo = submission?.Repo ?? submission?.Group?.Repo;
        var group = submission?.Group is null
            ? null
            : AssignmentGroupDto.From(submission.Group);
        return new StudentContentItem(
            link.Link.Id,
            link.Content.Id,
            link.Content.Type,
            link.Content.Assignment is null
                ? null
                : link.Content.Assignment.GradeScale,
            link.Content.Title,
            isLocked
                ? null
                : link.Content.BodyMd,
            link.Link.PublishAt,
            link.Link.DueAt,
            link.Link.IsPinned,
            isLocked,
            IsSatisfied(link.Link.Id, byLinkId, completionByLinkId, submissionByLinkId),
            PartRules.IsManual(link.Content),
            blockers,
            link.Content.Assignment is null
                ? null
                : submission?.Status,
            link.Content.Assignment is null
                ? null
                : submission?.Grade?.Level,
            link.Content.Assignment?.SubmissionModes.Contains(SubmissionMode.Repo) == true,
            link.Content.Assignment?.SubmissionModes ?? [],
            link.Content.Assignment?.Requirements
                .OrderBy(requirement => requirement.Position)
                .Select(AssignmentRequirementDto.From)
                .ToList() ?? [],
            repo is null
                ? null
                : StudentRepoDto.From(repo),
            group);
    }

    // Titeln visas bara om förutsättningen är publicerad och har passerat PublishAt.
    private static string? TitleOfBlocker(
        Guid linkId, DateTimeOffset now, Dictionary<Guid, LinkedContent> byLinkId) =>
        byLinkId.TryGetValue(linkId, out var blocker) && IsVisible(blocker, now)
            ? blocker.Content.Title
            : null;

    private static bool IsSatisfied(
        Guid linkId,
        Dictionary<Guid, LinkedContent> byLinkId,
        Dictionary<Guid, PartCompletion> completionByLinkId,
        Dictionary<Guid, Submission> submissionByLinkId)
    {
        if (!byLinkId.TryGetValue(linkId, out var link))
        {
            return false;
        }

        return PartRules.IsSatisfied(
            link.Content,
            completionByLinkId.GetValueOrDefault(linkId),
            submissionByLinkId.GetValueOrDefault(linkId));
    }

    private static bool IsVisible(LinkedContent link, DateTimeOffset now) =>
        ContentVisibility.IsVisible(link.Content, link.Link, now);

    private static bool IsModuleAvailable(Module module, DateTimeOffset now) =>
        ContentVisibility.IsModuleAvailable(module.PublishAt, now);

    private async Task<List<string>> MissingBlockersAsync(
        IReadOnlyList<Guid> groupIds, Guid courseId, Guid moduleId, Guid linkId, CancellationToken ct)
    {
        var dependencies = (await contents.GetDependenciesForModuleAsync(moduleId, ct))
            .Where(dependency => dependency.BlockedLinkId == linkId)
            .ToList();
        if (dependencies.Count == 0)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var links = await LinksForGroupsAsync(groupIds, courseId, ct);
        var byLinkId = links.ToDictionary(link => link.Link.Id);
        var linkIds = links.Select(link => link.Link.Id).ToList();
        var studentCompletions = await completions.GetForStudentAsync(currentUser.UserId, linkIds, ct);
        var studentSubmissions = await submissions.GetForStudentAsync(currentUser.UserId, linkIds, ct);
        var completionByLinkId = studentCompletions.ToDictionary(completion => completion.ContentLinkId);
        var submissionByLinkId = studentSubmissions.ToDictionary(submission => submission.ContentLinkId);

        return dependencies
            .Where(dependency => !IsSatisfied(
                dependency.BlockingLinkId, byLinkId, completionByLinkId, submissionByLinkId))
            .Select(dependency => TitleOfBlocker(dependency.BlockingLinkId, now, byLinkId))
            .Select(title => title ?? "a part that is not published yet")
            .ToList();
    }
}
