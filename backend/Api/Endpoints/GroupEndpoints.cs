using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = app.MapGroup("/groups").WithTags("Groups");

        groups.MapGet("", async (GroupService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAllAsync(ct)));

        // Vilka kurser läser en grupp? Tom lista = fristående grupp (t.ex. committee).
        groups.MapGet("/{id:guid}/courses", async (Guid id, GroupService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetCoursesAsync(id, ct)));

        groups.MapPost("/{id:guid}/courses/{courseId:guid}", async Task<Results<Created, Conflict>> (
            Guid id, Guid courseId, GroupService service, CancellationToken ct) =>
            await service.AddCourseAsync(id, courseId, ct)
                ? TypedResults.Created()
                : TypedResults.Conflict());

        return app;
    }
}
