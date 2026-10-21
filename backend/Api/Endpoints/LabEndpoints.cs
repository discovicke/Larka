using Backend.Application.Dtos;
using Backend.Application.Services;

namespace Backend.Api.Endpoints;

// --- labs (Classroom-ersättare, Fake nu / Octokit sen) ---
public static class LabEndpoints
{
    public static IEndpointRouteBuilder MapLabEndpoints(this IEndpointRouteBuilder app)
    {
        var courseLabs = app.MapGroup("/courses/{id:guid}/labs").WithTags("Labs");

        courseLabs.MapGet("", async (Guid id, LabService service, CancellationToken ct) =>
            Results.Ok(await service.GetByCourseAsync(id, ct)));

        courseLabs.MapPost("", async (Guid id, CreateLabDto d, LabService service, CancellationToken ct) =>
                await service.CreateAsync(id, d, ct) is { } lab
                    ? Results.Created($"/labs/{lab.Id}", lab)
                    : Results.BadRequest("Group does not read this course (missing GroupCourse)."));

        var labs = app.MapGroup("/labs").WithTags("Labs");

        labs.MapGet("/{id:guid}/repos", async (Guid id, LabService service, CancellationToken ct) =>
            Results.Ok(await service.GetReposAsync(id, ct)));

        labs.MapPost("/refresh-checks", async (LabService service, CancellationToken ct) =>
        {
            await service.RefreshChecksAsync(ct);
            return Results.Ok();
        });

        return app;
    }
}
