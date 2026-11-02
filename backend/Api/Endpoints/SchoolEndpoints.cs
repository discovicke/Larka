using Backend.Application.Dtos;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class SchoolEndpoints
{
    public static IEndpointRouteBuilder MapSchoolEndpoints(this IEndpointRouteBuilder app)
    {
        var schools = app.MapGroup("/schools").WithTags("Schools");

        schools.MapGet("", async (SchoolService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAllAsync(ct)));

        schools.MapGet("/{id:guid}", async Task<Results<Ok<SchoolDto>, NotFound>> (
            Guid id, SchoolService service, CancellationToken ct) =>
        {
            var school = await service.GetAsync(id, ct);
            return school is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(school);
        });

        schools.MapPost("", async (CreateSchoolDto schoolDto, SchoolService service, CancellationToken ct) =>
            (await service.CreateAsync(schoolDto, ct)).ToHttp(school =>
                TypedResults.Created($"/api/schools/{school.Id}", school)));

        // Microsoft-inloggning för skolan. Bara admin i skolan.
        schools.MapGet("/{id:guid}/microsoft", async (Guid id, SchoolService service, CancellationToken ct) =>
            (await service.GetMicrosoftAsync(id, ct)).ToHttp(microsoft => TypedResults.Ok(microsoft)));

        schools.MapPut("/{id:guid}/microsoft", async (Guid id, SchoolService service, CancellationToken ct) =>
            (await service.LinkMicrosoftAsync(id, ct)).ToHttp(microsoft => TypedResults.Ok(microsoft)));

        schools.MapDelete("/{id:guid}/microsoft", async (Guid id, SchoolService service, CancellationToken ct) =>
            (await service.UnlinkMicrosoftAsync(id, ct)).ToHttp(microsoft => TypedResults.Ok(microsoft)));

        return app;
    }
}
