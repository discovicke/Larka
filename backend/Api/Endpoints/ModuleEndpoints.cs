using Backend.Application.Dtos;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class ModuleEndpoints
{
    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder app)
    {
        var modules = app.MapGroup("/courses/{id:guid}/modules").WithTags("Modules");

        modules.MapGet("", async Task<Results<Ok<List<ModuleWithContentDto>>, NotFound>> (
            Guid id, ModuleService service, CancellationToken ct) =>
        {
            var courseModules = await service.GetForCourseAsync(id, ct);
            return courseModules is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(courseModules);
        });

        modules.MapPost("", async (Guid id, CreateModuleDto moduleDto, ModuleService service, CancellationToken ct) =>
            (await service.CreateAsync(id, moduleDto, ct)).ToHttp(module =>
                TypedResults.Created($"/api/courses/{id}/modules", module)));

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
