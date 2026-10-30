using Backend.Application.Dtos;
using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class ModuleEndpoints
{
    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder app)
    {
        var modules = app.MapGroup("/courses/{id:guid}/modules").WithTags("Modules");

        modules.MapGet("", async (Guid id, ModuleService service, CancellationToken ct) =>
            (await service.GetForCourseAsync(id, ct)).ToHttp(TypedResults.Ok));

        modules.MapPost("", async (Guid id, CreateModuleDto moduleDto, ModuleService service, CancellationToken ct) =>
            (await service.CreateAsync(id, moduleDto, ct)).ToHttp(module =>
                TypedResults.Created($"/api/courses/{id}/modules", module)));

        modules.MapPost("/copy", async (
            Guid id, CopyWeeksDto copyDto, CourseCopyService service, CancellationToken ct) =>
            (await service.CopyWeeksAsync(id, copyDto, ct)).ToHttp(result =>
                TypedResults.Created($"/api/courses/{id}/modules", result)));

        modules.MapPut("/{moduleId:guid}", async (
            Guid id, Guid moduleId, UpdateModuleDto moduleDto, ModuleService service, CancellationToken ct) =>
            (await service.UpdateAsync(id, moduleId, moduleDto, ct)).ToHttp(TypedResults.Ok));

        modules.MapDelete("/{moduleId:guid}", async (
            Guid id, Guid moduleId, ModuleService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, moduleId, ct)).ToHttp(_ => TypedResults.NoContent()));

        modules.MapPut("/order",
            async (Guid id, ReorderModulesDto reorderDto, ModuleService service, CancellationToken ct) =>
                (await service.ReorderAsync(id, reorderDto, ct)).ToHttp(TypedResults.Ok));

        modules.MapPut("/{moduleId:guid}/content/order", async (
            Guid id, Guid moduleId, ReorderContentDto reorderDto, ContentService service, CancellationToken ct) =>
            (await service.ReorderModuleContentAsync(id, moduleId, reorderDto, ct))
                .ToHttp(_ => TypedResults.NoContent()));

        modules.MapPut("/{moduleId:guid}/roadmap", async (
            Guid id, Guid moduleId, SaveRoadmapDto roadmapDto, RoadmapService service, CancellationToken ct) =>
            (await service.SaveAsync(id, moduleId, roadmapDto, ct)).ToHttp(TypedResults.Ok));

        return app;
    }
}
