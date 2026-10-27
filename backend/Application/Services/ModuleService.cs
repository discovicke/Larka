using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

// Kursens moduler (veckor). Innehåll läggs i en modul när det publiceras (CreateContentLinkDto.ModuleId).
public class ModuleService(
    ICourseRepository courses,
    IModuleRepository modules,
    IContentRepository contents,
    ISubmissionRepository submissions,
    IPartCompletionRepository completions,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    ILogger<ModuleService> logger)
{
    /// <summary>Returnerar null om kursen inte finns.</summary>
    public async Task<List<ModuleWithContentDto>?> GetForCourseAsync(Guid courseId, CancellationToken ct)
    {
        if (await courses.GetAsync(courseId, ct) is null)
        {
            return null;
        }

        var courseModules = await modules.GetForCourseAsync(courseId, ct);
        var content = await modules.GetContentByModuleAsync(courseId, ct);
        return courseModules
            .Select(module => ModuleWithContentDto.From(module, content[module.Id].ToList()))
            .ToList();
    }

    /// <summary>Den nya modulen hamnar sist i kursen.</summary>
    public async Task<ServiceResult<ModuleDto>> CreateAsync(Guid courseId, CreateModuleDto dto, CancellationToken ct)
    {
        if (await courses.GetAsync(courseId, ct) is null)
        {
            return ServiceResult<ModuleDto>.NotFound("course not found");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ServiceResult<ModuleDto>.Invalid("title is required");
        }

        var courseModules = await modules.GetForCourseAsync(courseId, ct);
        var module = new Module
        {
            CourseId = courseId,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? "",
            Position = courseModules.Count + 1,
            PublishAt = dto.PublishAt
        };
        modules.Add(module);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ModuleDto>.Ok(ModuleDto.From(module));
    }

    public async Task<ServiceResult<ModuleDto>> UpdateAsync(
        Guid courseId, Guid moduleId, UpdateModuleDto dto, CancellationToken ct)
    {
        if (await courses.GetAsync(courseId, ct) is null)
        {
            return ServiceResult<ModuleDto>.NotFound("course not found");
        }

        var module = await modules.GetAsync(moduleId, ct);
        if (module is null || module.CourseId != courseId)
        {
            return ServiceResult<ModuleDto>.NotFound("module not found");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ServiceResult<ModuleDto>.Invalid("title is required");
        }

        module.Title = dto.Title.Trim();
        module.Description = dto.Description?.Trim() ?? "";
        module.PublishAt = dto.PublishAt;
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ModuleDto>.Ok(ModuleDto.From(module));
    }

    /// <summary>
    /// Tar bort veckan och allt som bara hör till den. Elevarbete blockerar, men en publicerad
    /// inlämning utan elevaktivitet får tas bort efter att klienten har bekräftat veckans titel.
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteAsync(Guid courseId, Guid moduleId, CancellationToken ct)
    {
        if (await courses.GetAsync(courseId, ct) is null)
        {
            return ServiceResult<bool>.NotFound("course not found");
        }

        var module = await modules.GetAsync(moduleId, ct);
        if (module is null || module.CourseId != courseId)
        {
            return ServiceResult<bool>.NotFound("module not found");
        }

        var parts = await contents.GetLinksWithContentForModuleAsync(moduleId, ct);
        foreach (var part in parts)
        {
            var linkSubmissions = await submissions.GetByLinkAsync(part.Link.Id, ct);
            if (linkSubmissions.Any(StudentWorkPolicy.HasStudentActivity)
                || await completions.AnyForLinkAsync(part.Link.Id, ct))
            {
                return ServiceResult<bool>.Conflict(
                    $"the week contains student activity in \"{part.Content.Title}\" and cannot be deleted");
            }
        }

        // Content som också finns i en annan vecka eller grupp ska leva vidare. Räkna före borttagningen,
        // och hantera att samma content kan vara länkat flera gånger i samma vecka.
        var linksByContentId = parts
            .GroupBy(part => part.Content.Id)
            .ToDictionary(group => group.Key, group => group.Count());
        var contentById = parts
            .GroupBy(part => part.Content.Id)
            .ToDictionary(group => group.Key, group => group.First().Content);
        var contentToDelete = new HashSet<Guid>();
        var attachments = new List<Attachment>();
        foreach (var (contentId, linksInWeek) in linksByContentId)
        {
            var totalLinks = await contents.CountLinksAsync(contentId, ct);
            if (totalLinks != linksInWeek)
            {
                continue;
            }

            contentToDelete.Add(contentId);
            attachments.AddRange(await contents.GetAttachmentsAsync(contentId, ct));
        }

        foreach (var part in parts)
        {
            // Aktivitet har redan stoppat raderingen, så det som är kvar är tomma utdelningsrader.
            foreach (var submission in await submissions.GetByLinkAsync(part.Link.Id, ct))
            {
                submissions.Remove(submission);
            }

            contents.RemoveLink(part.Link);
        }

        foreach (var contentId in contentToDelete)
        {
            contents.RemoveContent(contentById[contentId]);
        }

        var courseModules = await modules.GetForCourseAsync(courseId, ct);
        var remaining = courseModules
            .Where(courseModule => courseModule.Id != moduleId)
            .OrderBy(courseModule => courseModule.Position)
            .ToList();
        for (var i = 0; i < remaining.Count; i++)
        {
            remaining[i].Position = i + 1;
        }

        modules.Remove(module);
        await unitOfWork.SaveChangesAsync(ct);
        foreach (var attachment in attachments)
        {
            await DeleteStorageFileQuietlyAsync(attachment.StorageKey);
        }

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<List<ModuleDto>>> ReorderAsync(
        Guid courseId, ReorderModulesDto dto, CancellationToken ct)
    {
        if (await courses.GetAsync(courseId, ct) is null)
        {
            return ServiceResult<List<ModuleDto>>.NotFound("course not found");
        }

        var courseModules = (await modules.GetForCourseAsync(courseId, ct)).ToDictionary(module => module.Id);
        var listsEveryModuleOnce = dto.ModuleIds.Count == courseModules.Count
            && dto.ModuleIds.Distinct().Count() == courseModules.Count
            && dto.ModuleIds.All(courseModules.ContainsKey);
        if (!listsEveryModuleOnce)
        {
            return ServiceResult<List<ModuleDto>>.Invalid("moduleIds must list every module in the course once");
        }

        for (var i = 0; i < dto.ModuleIds.Count; i++)
        {
            courseModules[dto.ModuleIds[i]].Position = i + 1;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<List<ModuleDto>>.Ok(
            dto.ModuleIds.Select(id => ModuleDto.From(courseModules[id])).ToList());
    }

    private async Task DeleteStorageFileQuietlyAsync(string key)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not delete {StorageKey} from file storage", key);
        }
    }
}
