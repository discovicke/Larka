using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Kursens moduler (veckor). Innehåll läggs i en modul när det publiceras (CreateContentLinkDto.ModuleId).
public class ModuleService(
    ICourseRepository courses,
    IModuleRepository modules,
    IContentRepository contents,
    ISubmissionRepository submissions,
    IPartCompletionRepository completions,
    IPermissions permissions,
    NotificationService notifications,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    ILogger<ModuleService> logger)
{
    /// <summary>
    /// Kursens veckor med innehåll. Läraren ser allt, med utkast, schemalagt och antal inlämningar. Admin i skolan
    /// läser också allt, men utan inlämningar. Mentor och elev ser bara det som är publicerat och öppet,
    /// och aldrig en vecka där alla delar är dolda.
    /// Övriga får 403.
    /// </summary>
    public async Task<ServiceResult<List<ModuleWithContentDto>>> GetForCourseAsync(Guid courseId, CancellationToken ct)
    {
        if (await courses.GetAsync(courseId, ct) is null)
        {
            return ServiceResult<List<ModuleWithContentDto>>.NotFound("course not found");
        }

        if (!await permissions.CanReadCourseAsync(courseId, ct))
        {
            return ServiceResult<List<ModuleWithContentDto>>.Forbidden();
        }

        var teaches = await permissions.CanTeachCourseAsync(courseId, ct);
        var seesEverything = teaches || await permissions.IsAdminAsync(ct);
        var now = time.GetUtcNow();
        var courseModules = await modules.GetForCourseAsync(courseId, ct);
        var content = await modules.GetContentByModuleAsync(courseId, teaches, ct);
        return ServiceResult<List<ModuleWithContentDto>>.Ok(
            courseModules
                .Where(module => seesEverything || ContentVisibility.IsModuleAvailable(module.PublishAt, now))
                .Where(module => seesEverything
                    || !content[module.Id].Any()
                    || content[module.Id].Any(item => IsVisible(item, now)))
                .Select(module => ModuleWithContentDto.From(
                    module,
                    content[module.Id]
                        .Where(item => seesEverything || IsVisible(item, now))
                        .ToList()))
                .ToList());
    }

    private static bool IsVisible(ModuleContentItem item, DateTimeOffset now) =>
        item.Status == ContentStatus.Published && item.PublishAt <= now;

    /// <summary>
    /// Publicerar veckornas utkast. Delarna syns för eleverna på sina egna datum, och notiserna kommer när de
    /// blir synliga. Veckor utan utkast räknas inte.
    /// </summary>
    public async Task<ServiceResult<PublishWeeksResultDto>> PublishAsync(
        Guid courseId, PublishWeeksDto dto, CancellationToken ct)
    {
        if (await courses.GetAsync(courseId, ct) is null)
        {
            return ServiceResult<PublishWeeksResultDto>.NotFound("course not found");
        }

        if (!await permissions.CanTeachCourseAsync(courseId, ct))
        {
            return ServiceResult<PublishWeeksResultDto>.Forbidden();
        }

        var weeks = await modules.GetForCourseAsync(courseId, ct);
        if (dto.ModuleIds is not null)
        {
            weeks = weeks.Where(week => dto.ModuleIds.Contains(week.Id)).ToList();
            if (weeks.Count != dto.ModuleIds.Distinct().Count())
            {
                return ServiceResult<PublishWeeksResultDto>.Invalid(
                    "moduleIds must only contain weeks in the course");
            }
        }

        var weekCount = 0;
        var partCount = 0;
        foreach (var week in weeks)
        {
            var drafts = (await contents.GetLinksWithContentForModuleAsync(week.Id, ct))
                .Where(part => part.Content.Status == ContentStatus.Draft)
                .ToList();
            if (drafts.Count > 0)
            {
                weekCount++;
            }

            foreach (var part in drafts)
            {
                part.Content.Status = ContentStatus.Published;
                await notifications.NotifyContentPublishedAsync(part.Content, ct);
                partCount++;
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<PublishWeeksResultDto>.Ok(new PublishWeeksResultDto(weekCount, partCount));
    }

    /// <summary>Den nya modulen hamnar sist i kursen.</summary>
    public async Task<ServiceResult<ModuleDto>> CreateAsync(Guid courseId, CreateModuleDto dto, CancellationToken ct)
    {
        if (await courses.GetAsync(courseId, ct) is null)
        {
            return ServiceResult<ModuleDto>.NotFound("course not found");
        }

        if (!await permissions.CanTeachCourseAsync(courseId, ct))
        {
            return ServiceResult<ModuleDto>.Forbidden();
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

        if (!await permissions.CanTeachCourseAsync(courseId, ct))
        {
            return ServiceResult<ModuleDto>.Forbidden();
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

        if (!await permissions.CanTeachCourseAsync(courseId, ct))
        {
            return ServiceResult<bool>.Forbidden();
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
            if (await StudentWorkPolicy.LinkHasStudentWorkAsync(linkSubmissions, completions, part.Link.Id, ct))
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
        PositionOrder.Renumber(remaining);

        modules.Remove(module);
        await unitOfWork.SaveChangesAsync(ct);
        foreach (var attachment in attachments)
        {
            await storage.DeleteQuietlyAsync(attachment.StorageKey, logger);
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

        if (!await permissions.CanTeachCourseAsync(courseId, ct))
        {
            return ServiceResult<List<ModuleDto>>.Forbidden();
        }

        var courseModules = (await modules.GetForCourseAsync(courseId, ct)).ToDictionary(module => module.Id);
        var listsEveryModuleOnce = dto.ModuleIds.Count == courseModules.Count
            && dto.ModuleIds.Distinct().Count() == courseModules.Count
            && dto.ModuleIds.All(courseModules.ContainsKey);
        if (!listsEveryModuleOnce)
        {
            return ServiceResult<List<ModuleDto>>.Invalid("moduleIds must list every module in the course once");
        }

        PositionOrder.Renumber(dto.ModuleIds.Select(moduleId => courseModules[moduleId]));

        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<List<ModuleDto>>.Ok(
            dto.ModuleIds.Select(id => ModuleDto.From(courseModules[id])).ToList());
    }
}
