using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Veckans roadmap: vilka delar som är låsta av vilka andra delar, och när en uppgift med
// inlämningssätt räknas som klar. Allt sparas i en transaktion så att veckan aldrig hamnar i halvt skick.
public class RoadmapService(
    IContentRepository contents,
    IModuleRepository modules,
    IGroupRepository groups,
    IPermissions permissions,
    IUnitOfWork unitOfWork,
    ModuleService moduleService)
{
    public async Task<ServiceResult<ModuleWithContentDto>> SaveAsync(
        Guid courseId, Guid moduleId, SaveRoadmapDto dto, CancellationToken ct)
    {
        if (!await groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<ModuleWithContentDto>.NotFound("course not found");
        }

        if (!await permissions.CanTeachCourseAsync(courseId, ct))
        {
            return ServiceResult<ModuleWithContentDto>.Forbidden();
        }

        var module = await modules.GetAsync(moduleId, ct);
        if (module is null || module.CourseId != courseId)
        {
            return ServiceResult<ModuleWithContentDto>.NotFound("module not found");
        }

        if (dto.Links is null)
        {
            return ServiceResult<ModuleWithContentDto>.Invalid("links is required");
        }

        var parts = await contents.GetLinksWithContentForModuleAsync(moduleId, ct);
        var partsById = parts.ToDictionary(part => part.Link.Id);
        var listsEveryPartOnce = dto.Links.Count == parts.Count
            && dto.Links.Select(link => link.LinkId).Distinct().Count() == parts.Count
            && dto.Links.All(link => partsById.ContainsKey(link.LinkId));
        if (!listsEveryPartOnce)
        {
            return ServiceResult<ModuleWithContentDto>.Invalid(
                "links must list every part in the week once");
        }

        foreach (var link in dto.Links)
        {
            var error = CheckLink(link, partsById);
            if (error is not null)
            {
                return ServiceResult<ModuleWithContentDto>.Invalid(error);
            }
        }

        var blockedBy = dto.Links.ToDictionary(link => link.LinkId, link => link.BlockedByLinkIds);
        var cycle = FindCycle(blockedBy);
        if (cycle is not null)
        {
            var titles = cycle.Select(linkId => partsById[linkId].Content.Title);
            return ServiceResult<ModuleWithContentDto>.Invalid($"cycle: {string.Join(" → ", titles)}");
        }

        contents.RemoveDependencies(await contents.GetDependenciesForModuleAsync(moduleId, ct));
        foreach (var link in dto.Links)
        {
            foreach (var blockingId in link.BlockedByLinkIds)
            {
                contents.AddDependency(new ContentLinkDependency
                {
                    BlockedLinkId = link.LinkId,
                    BlockingLinkId = blockingId
                });
            }

            var content = partsById[link.LinkId].Content;
            // Ett utkast som läraren har med i roadmapen publiceras när roadmapen sparas.
            // Annars kan eleven få en vecka som aldrig går att göra klar.
            if (content.Status == ContentStatus.Draft)
            {
                content.Status = ContentStatus.Published;
            }

            if (link.CompletionRule is not null)
            {
                content.Assignment!.CompletionRule = link.CompletionRule.Value;
            }
        }

        // Listan ska följa roadmapen, oavsett i vilken ordning delarna skapades. En del hamnar
        // alltid efter sina förutsättningar, och i övrigt behålls lärarens ordning.
        var ordered = OrderByDependencies(parts, blockedBy);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Link.Position = i + 1;
        }

        await unitOfWork.SaveChangesAsync(ct);

        var courseModules = await moduleService.GetForCourseAsync(courseId, ct);
        return ServiceResult<ModuleWithContentDto>.Ok(
            courseModules!.Single(savedModule => savedModule.Id == moduleId));
    }

    // Tar fram veckans ordning utifrån beroendena. Algoritmen tar hela tiden den tidigaste
    // tillgängliga delen, så att lärarens nuvarande ordning behålls när beroendena tillåter.
    // Cykler har redan avvisats innan den här metoden anropas.
    private static List<LinkedContent> OrderByDependencies(
        List<LinkedContent> parts, Dictionary<Guid, List<Guid>> blockedBy)
    {
        var partsById = parts.ToDictionary(part => part.Link.Id);
        var originalIndex = parts
            .Select((part, index) => (part.Link.Id, Index: index))
            .ToDictionary(entry => entry.Id, entry => entry.Index);
        var remainingBlockers = parts.ToDictionary(
            part => part.Link.Id,
            part => blockedBy.GetValueOrDefault(part.Link.Id)?.Count ?? 0);
        var unlocks = parts.ToDictionary(part => part.Link.Id, _ => new List<Guid>());
        foreach (var (blockedId, blockingIds) in blockedBy)
        {
            foreach (var blockingId in blockingIds)
            {
                unlocks[blockingId].Add(blockedId);
            }
        }

        var ready = new PriorityQueue<Guid, int>();
        foreach (var part in parts.Where(part => remainingBlockers[part.Link.Id] == 0))
        {
            ready.Enqueue(part.Link.Id, originalIndex[part.Link.Id]);
        }

        var ordered = new List<LinkedContent>(parts.Count);
        while (ready.TryDequeue(out var linkId, out _))
        {
            ordered.Add(partsById[linkId]);
            foreach (var blockedId in unlocks[linkId])
            {
                remainingBlockers[blockedId]--;
                if (remainingBlockers[blockedId] == 0)
                {
                    ready.Enqueue(blockedId, originalIndex[blockedId]);
                }
            }
        }

        return ordered;
    }

    // Null om raden är giltig, annars varför den inte är det.
    private static string? CheckLink(RoadmapLinkDto link, Dictionary<Guid, LinkedContent> partsById)
    {
        if (link.BlockedByLinkIds is null)
        {
            return "blockedByLinkIds is required";
        }

        if (link.BlockedByLinkIds.Distinct().Count() != link.BlockedByLinkIds.Count)
        {
            return "blockedByLinkIds must not contain duplicates";
        }

        if (link.BlockedByLinkIds.Contains(link.LinkId))
        {
            return "a part cannot be blocked by itself";
        }

        if (link.BlockedByLinkIds.Any(linkId => !partsById.ContainsKey(linkId)))
        {
            return "blockedByLinkIds must only contain parts in the same week";
        }

        var part = partsById[link.LinkId].Content;
        if (link.BlockedByLinkIds.Any(linkId => partsById[linkId].Content.Type == ContentType.News))
        {
            return "news cannot be a blocker";
        }

        // Nyheter får ligga i veckan, men de är inte en del av roadmapen och kan därför varken
        // låsa upp något eller låsas av något.
        if (part.Type == ContentType.News && link.BlockedByLinkIds.Count > 0)
        {
            return "news cannot be part of the roadmap";
        }

        return CheckCompletionRule(part, link.CompletionRule);
    }

    // Regeln hör till uppgiften: teori och uppgifter utan inlämningssätt klarmarkeras alltid manuellt.
    private static string? CheckCompletionRule(Content content, CompletionRule? rule)
    {
        if (rule is null)
        {
            return null;
        }

        var spec = content.Assignment;
        if (content.Type != ContentType.Assignment || spec is null || spec.SubmissionModes.Count == 0)
        {
            return "completionRule is only allowed on assignments with submission modes";
        }

        return rule == CompletionRule.Approved && spec.GradeScale == GradeScale.None
            ? "completionRule Approved requires a grade scale"
            : null;
    }

    /// <summary>
    /// Cykeln som en lista av länk-id:n, där första och sista är samma del, eller null om grafen är acyklisk.
    /// </summary>
    private static List<Guid>? FindCycle(Dictionary<Guid, List<Guid>> blockedBy)
    {
        var state = new Dictionary<Guid, int>(); // 1 = på vägen, 2 = färdig
        var path = new List<Guid>();
        foreach (var linkId in blockedBy.Keys)
        {
            var cycle = Visit(linkId, blockedBy, state, path);
            if (cycle is not null)
            {
                return cycle;
            }
        }

        return null;
    }

    private static List<Guid>? Visit(
        Guid linkId, Dictionary<Guid, List<Guid>> blockedBy, Dictionary<Guid, int> state, List<Guid> path)
    {
        state[linkId] = 1;
        path.Add(linkId);
        foreach (var blockingId in blockedBy.GetValueOrDefault(linkId) ?? [])
        {
            var blockingState = state.GetValueOrDefault(blockingId);
            if (blockingState == 1)
            {
                return path.Skip(path.IndexOf(blockingId)).Append(blockingId).ToList();
            }

            if (blockingState == 0)
            {
                var cycle = Visit(blockingId, blockedBy, state, path);
                if (cycle is not null)
                {
                    return cycle;
                }
            }
        }

        state[linkId] = 2;
        path.RemoveAt(path.Count - 1);
        return null;
    }
}
