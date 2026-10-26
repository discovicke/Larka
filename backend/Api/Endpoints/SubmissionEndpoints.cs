using Backend.Application.Dtos;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static IEndpointRouteBuilder MapSubmissionEndpoints(this IEndpointRouteBuilder app)
    {
        // --- inlämningar per utdelning (ContentLink) av en uppgift ---
        var links = app.MapGroup("/links/{id:guid}/submissions").WithTags("Submissions");

        links.MapGet("", async (Guid id, SubmissionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetByLinkAsync(id, ct)));

        // Samma anrop för första inlämningen och för komplettering. Filer kan inte skickas hit än (#47).
        links.MapPost("", async (Guid id, SubmitDto submitDto, SubmissionService service, CancellationToken ct) =>
            (await service.SubmitAsync(id, submitDto, null, ct)).ToHttp(submission => TypedResults.Ok(submission)));

        var submissions = app.MapGroup("/submissions").WithTags("Submissions");

        submissions.MapGet("/{id:guid}", async Task<Results<Ok<SubmissionDto>, NotFound>> (
            Guid id, SubmissionService service, CancellationToken ct) =>
        {
            var submission = await service.GetAsync(id, ct);
            return submission is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(submission);
        });

        submissions.MapPost("/{id:guid}/grade",
            async (Guid id, GradeSubmissionDto gradeDto, SubmissionService service, CancellationToken ct) =>
                (await service.GradeAsync(id, gradeDto, ct)).ToHttp(submission => TypedResults.Ok(submission)));

        // --- GitHub-repon (Fake nu / Octokit sen) ---
        submissions.MapPost("/refresh-checks", async (SubmissionService service, CancellationToken ct) =>
        {
            await service.RefreshChecksAsync(ct);
            return TypedResults.Ok();
        });

        return app;
    }
}
