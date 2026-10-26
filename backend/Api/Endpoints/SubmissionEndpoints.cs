using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static IEndpointRouteBuilder MapSubmissionEndpoints(this IEndpointRouteBuilder app)
    {
        // --- inlämningar per utdelning (ContentLink) av en uppgift ---
        var links = app.MapGroup("/links/{id:guid}/submissions").WithTags("Submissions");

        links.MapGet("", async (Guid id, SubmissionService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetByLinkAsync(id, ct)));

        // Text och filer i samma form (multipart/form-data), som formuläret skickar dem.
        links.MapPost("", async (
            Guid id, [FromForm] SubmitForm form, SubmissionService service, CancellationToken ct) =>
        {
            var result = await FormFileUploads.WithUploadsAsync(form.Files, uploads =>
                service.SubmitAsync(id, new SubmitDto(form.BodyMd), uploads, ct));
            return result.ToHttp(submission => TypedResults.Ok(submission));
        })
            .Accepts<SubmitForm>("multipart/form-data")
            .DisableAntiforgery() // inloggning med cookies finns inte, så inget CSRF-skydd behövs
            .WithMetadata(new RequestSizeLimitAttribute((int)FileUploadPolicy.MaxRequestBytes));

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
