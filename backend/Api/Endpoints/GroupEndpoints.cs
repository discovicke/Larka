using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = app.MapGroup("/groups").WithTags("Groups");

        groups.MapGet("", async (GroupService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        // Vilka kurser läser en grupp? Tom lista = fristående grupp (t.ex. committee).
        groups.MapGet("/{id:guid}/courses", async (Guid id, GroupService service, CancellationToken ct) =>
            Results.Ok(await service.GetCoursesAsync(id, ct)));

        groups.MapPost("/{id:guid}/courses/{courseId:guid}",
                async (Guid id, Guid courseId, GroupService service, CancellationToken ct) =>
                    await service.AddCourseAsync(id, courseId, ct)
                        ? Results.Created()
                        : Results.Conflict());

        return app;
    }
}
