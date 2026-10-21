using Backend.Application.Dtos;
using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static IEndpointRouteBuilder MapSubmissionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/posts/{id:guid}/submissions",
                async (Guid id, SubmitDto d, SubmissionService service, CancellationToken ct) =>
                {
                    var submission = await service.SubmitAsync(id, d, ct);
                    return Results.Created($"/submissions/{submission.Id}", submission);
                })
            .WithTags("Submissions");

        app.MapPost("/submissions/{id:guid}/grade",
                async (Guid id, GradeDto d, SubmissionService service, CancellationToken ct) =>
                {
                    await service.GradeAsync(id, d, ct);
                    return Results.Ok();
                })
            .WithTags("Submissions");

        return app;
    }
}
