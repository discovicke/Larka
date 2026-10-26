using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

// Kursens moduler (veckor). Innehåll läggs i en modul när det publiceras (CreateContentLinkDto.ModuleId).
public class ModuleService(ICourseRepository courses, IModuleRepository modules, IUnitOfWork unitOfWork)
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
            Position = courseModules.Count + 1
        };
        modules.Add(module);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<ModuleDto>.Ok(ModuleDto.From(module));
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
}
