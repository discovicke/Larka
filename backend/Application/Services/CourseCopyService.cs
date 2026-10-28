using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Kopierar veckor med delar från en annan kurs. Kopian är självständig: nya moduler, nytt innehåll, egna
// bilagefiler och beroenden mellan de nya delarna. Inlämningar, betyg, kommentarer, klarmarkeringar och
// notiser hör till eleverna och kopieras aldrig. Allt sparas i en transaktion; filkopiorna städas bort om
// sparandet misslyckas.
public class CourseCopyService(
    ICourseRepository courses,
    IModuleRepository modules,
    IContentRepository contents,
    IPermissions permissions,
    ICurrentUser currentUser,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    ILogger<CourseCopyService> logger)
{
    /// <summary>
    /// Veckorna hamnar sist i målkursen i källkursens ordning. Delarna börjar som utkast, och datumen (veckans
    /// och delarnas publicering och deadline) flyttas lika mycket som kursernas startdatum skiljer sig åt.
    /// </summary>
    public async Task<ServiceResult<CopyWeeksResultDto>> CopyWeeksAsync(
        Guid targetCourseId, CopyWeeksDto dto, CancellationToken ct)
    {
        var target = await courses.GetAsync(targetCourseId, ct);
        var source = await courses.GetAsync(dto.SourceCourseId, ct);
        if (target is null || source is null)
        {
            return ServiceResult<CopyWeeksResultDto>.NotFound("course not found");
        }

        if (!await permissions.CanTeachCourseAsync(targetCourseId, ct)
            || !await permissions.CanTeachCourseAsync(source.Id, ct))
        {
            return ServiceResult<CopyWeeksResultDto>.Forbidden();
        }

        if (dto.ModuleIds is null || dto.ModuleIds.Count == 0
            || dto.ModuleIds.Distinct().Count() != dto.ModuleIds.Count)
        {
            return ServiceResult<CopyWeeksResultDto>.Invalid("moduleIds must list at least one week, once each");
        }

        var sourceModules = await modules.GetForCourseAsync(source.Id, ct);
        var selected = sourceModules.Where(module => dto.ModuleIds.Contains(module.Id)).ToList();
        if (selected.Count != dto.ModuleIds.Count)
        {
            return ServiceResult<CopyWeeksResultDto>.Invalid("moduleIds must only contain weeks in the source course");
        }

        var shift = DateShift(source.StartsOn, target.StartsOn);
        var nextPosition = (await modules.GetForCourseAsync(targetCourseId, ct)).Count + 1;
        var plan = new CopyPlan();

        try
        {
            await AddCopiesAsync(selected, targetCourseId, nextPosition, shift, plan, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await DeleteCopiedFilesAsync(plan);
            throw;
        }

        return ServiceResult<CopyWeeksResultDto>.Ok(
            new CopyWeeksResultDto(plan.Modules.Select(ModuleDto.From).ToList(), plan.PartCount));
    }

    /// <summary>Det som en kopiering har lagt till i kontexten men ännu inte sparat.</summary>
    public class CopyPlan
    {
        public List<Module> Modules { get; } = [];
        public int PartCount { get; set; }
        public List<string> CopiedFiles { get; } = [];
    }

    /// <summary>
    /// Lägger kopiorna av veckorna i kontexten utan att spara, så att den som anropar kan spara dem
    /// tillsammans med annat. Misslyckas sparandet ska anroparen anropa DeleteCopiedFilesAsync.
    /// </summary>
    public async Task AddCopiesAsync(
        IReadOnlyList<Module> selected, Guid targetCourseId, int nextPosition, TimeSpan shift, CopyPlan plan,
        CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var contentCopies = new Dictionary<Guid, Content>();
        foreach (var module in selected)
        {
            var newModule = new Module
            {
                CourseId = targetCourseId,
                Title = module.Title,
                Description = module.Description,
                Position = nextPosition++,
                PublishAt = module.PublishAt + shift
            };
            modules.Add(newModule);
            plan.Modules.Add(newModule);

            var parts = await contents.GetLinksWithContentForModuleAsync(module.Id, ct);
            var newLinkIds = new Dictionary<Guid, Guid>();
            foreach (var part in parts)
            {
                if (!contentCopies.TryGetValue(part.Content.Id, out var content))
                {
                    content = CopyContent(part.Content, now);
                    contentCopies[part.Content.Id] = content;
                    contents.Add(content);
                    await CopyAttachmentsAsync(part.Content.Id, content.Id, plan.CopiedFiles, ct);
                }

                var link = new ContentLink
                {
                    ContentId = content.Id,
                    CourseId = targetCourseId,
                    ModuleId = newModule.Id,
                    Position = part.Link.Position,
                    PublishAt = part.Link.PublishAt + shift,
                    DueAt = part.Link.DueAt + shift,
                    IsPinned = part.Link.IsPinned
                };
                contents.AddLink(link);
                newLinkIds[part.Link.Id] = link.Id;
                plan.PartCount++;
            }

            foreach (var dependency in await contents.GetDependenciesForModuleAsync(module.Id, ct))
            {
                // Beroenden mellan veckor finns inte, men en dålig rad ska inte kunna ge ett fel här.
                if (newLinkIds.TryGetValue(dependency.BlockedLinkId, out var blocked)
                    && newLinkIds.TryGetValue(dependency.BlockingLinkId, out var blocking))
                {
                    contents.AddDependency(new ContentLinkDependency
                    {
                        BlockedLinkId = blocked,
                        BlockingLinkId = blocking
                    });
                }
            }
        }
    }

    public async Task DeleteCopiedFilesAsync(CopyPlan plan)
    {
        foreach (var key in plan.CopiedFiles)
        {
            await DeleteQuietlyAsync(key);
        }
    }

    /// <summary>Skillnaden mellan kursernas startdatum. Saknar någon kurs startdatum flyttas inget.</summary>
    public static TimeSpan DateShift(DateOnly? sourceStart, DateOnly? targetStart) =>
        sourceStart is null || targetStart is null
            ? TimeSpan.Zero
            : TimeSpan.FromDays(targetStart.Value.DayNumber - sourceStart.Value.DayNumber);

    private Content CopyContent(Content original, DateTimeOffset now)
    {
        var content = new Content
        {
            AuthorId = currentUser.UserId,
            Type = original.Type,
            Title = original.Title,
            BodyMd = original.BodyMd,
            Status = ContentStatus.Draft,
            CreatedAt = now
        };
        if (original.Assignment is not null)
        {
            content.Assignment = new AssignmentSpec
            {
                ContentId = content.Id,
                GradeScale = original.Assignment.GradeScale,
                SubmissionModes = [.. original.Assignment.SubmissionModes],
                CompletionRule = original.Assignment.CompletionRule,
                TemplateRepo = original.Assignment.TemplateRepo
            };
        }

        return content;
    }

    // Filen kopieras i lagringen, så att kopian överlever att originalet tas bort.
    private async Task CopyAttachmentsAsync(
        Guid sourceContentId, Guid newContentId, List<string> copiedFiles, CancellationToken ct)
    {
        foreach (var attachment in await contents.GetAttachmentsAsync(sourceContentId, ct))
        {
            var key = await storage.CopyAsync(attachment.StorageKey, $"content/{newContentId}", ct);
            copiedFiles.Add(key);
            contents.AddAttachment(new Attachment
            {
                ContentId = newContentId,
                FileName = attachment.FileName,
                StorageKey = key,
                SizeBytes = attachment.SizeBytes
            });
        }
    }

    private async Task DeleteQuietlyAsync(string key)
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
