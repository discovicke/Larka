using Backend.Application.Dtos;
using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static IEndpointRouteBuilder MapSubmissionEndpoints(this IEndpointRouteBuilder app)
    {
        // --- inlämningar per utdelning (ContentLink) av en uppgift ---
        var links = app.MapGroup("/links/{id:guid}/submissions").WithTags("Submissions");

        links.MapGet("", async (Guid id, SubmissionService service, CancellationToken ct) =>
            Results.Ok(await service.GetByLinkAsync(id, ct)));

        // Samma anrop för första inlämningen och för komplettering.
        links.MapPost("", async (Guid id, SubmitDto submitDto, SubmissionService service, CancellationToken ct) =>
            (await service.SubmitAsync(id, submitDto, ct)).ToHttp(Results.Ok));

        var submissions = app.MapGroup("/submissions").WithTags("Submissions");

        submissions.MapGet("/{id:guid}", async (Guid id, SubmissionService service, CancellationToken ct) =>
        {
            var submission = await service.GetAsync(id, ct);
            return submission is null
                ? Results.NotFound()
                : Results.Ok(submission);
        });

        submissions.MapPost("/{id:guid}/grade",
            async (Guid id, GradeDto gradeDto, SubmissionService service, CancellationToken ct) =>
                (await service.GradeAsync(id, gradeDto, ct)).ToHttp(Results.Ok));

        // --- GitHub-repon (Fake nu / Octokit sen) ---
        submissions.MapPost("/refresh-checks", async (SubmissionService service, CancellationToken ct) =>
        {
            await service.RefreshChecksAsync(ct);
            return Results.Ok();
        });

        return app;
    }
}
