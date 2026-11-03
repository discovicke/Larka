using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static IEndpointRouteBuilder MapSubmissionEndpoints(this IEndpointRouteBuilder app)
    {
        // --- inlämningar per utdelning (ContentLink) av en uppgift ---
        var links = app.MapGroup("/links/{id:guid}/submissions").WithTags("Submissions");

        links.MapGet("", async (Guid id, SubmissionService service, CancellationToken ct) =>
            (await service.GetByLinkAsync(id, ct)).ToHttp(TypedResults.Ok));

        // Text och filer i samma form (multipart/form-data), som formuläret skickar dem.
        links.MapPost("", async (
            Guid id, [FromForm] SubmitForm form, SubmissionService service, CancellationToken ct) =>
        {
            var result = await FormFileUploads.WithUploadsAsync(form.Files, uploads =>
                service.SubmitAsync(id, new SubmitDto(form.BodyMd, form.KeepFileIds), uploads, ct));
            return result.ToHttp(submission => TypedResults.Ok(submission));
        })
            .Accepts<SubmitForm>("multipart/form-data")
            .DisableAntiforgery() // inloggning med cookies finns inte, så inget CSRF-skydd behövs
            .WithMetadata(new RequestSizeLimitAttribute((int)FileUploadPolicy.MaxRequestBytes));

        links.MapPost("/mark-not-submitted", async (Guid id, SubmissionService service, CancellationToken ct) =>
            (await service.MarkAllNotSubmittedAsync(id, ct)).ToHttp(
                count => TypedResults.Ok(new MarkedNotSubmittedDto(count))));

        var submissions = app.MapGroup("/submissions").WithTags("Submissions");

        submissions.MapGet("/{id:guid}", async (Guid id, SubmissionService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttp(TypedResults.Ok));

        submissions.MapPost("/{id:guid}/grade",
            async (Guid id, GradeSubmissionDto gradeDto, SubmissionService service, CancellationToken ct) =>
                (await service.GradeAsync(id, gradeDto, ct)).ToHttp(submission => TypedResults.Ok(submission)));

        // --- GitHub-repon (Fake nu / Octokit sen) ---
        // Global check-refresh körs bara av GitHubChecksJob. Läraren uppdaterar en utdelning via
        // POST /links/{id}/repos/refresh, som behörighetsprövas.
        var repoActions = app.MapGroup("/links/{id:guid}/repos").WithTags("Submissions");

        repoActions.MapPost("/refresh", async (
            Guid id, SubmissionService service, CancellationToken ct) =>
            (await service.RefreshReposForLinkAsync(id, ct)).ToHttp(_ => TypedResults.NoContent()));

        repoActions.MapPost("/lock", async (
            Guid id, SubmissionService service, CancellationToken ct) =>
            (await service.LockReposForLinkAsync(id, ct)).ToHttp(_ => TypedResults.NoContent()));

        var help = app.MapGroup("/links/{id:guid}/help").WithTags("Student support");

        help.MapGet("", async (
            Guid id, StudentSupportService service, CancellationToken ct) =>
            (await service.GetHelpAsync(id, ct)).ToHttp(TypedResults.Ok));

        help.MapPost("", async (
            Guid id, SetStudentHelpDto dto, StudentSupportService service, CancellationToken ct) =>
            (await service.SetHelpAsync(id, dto, ct)).ToHttp(TypedResults.Ok));

        help.MapDelete("", async (
            Guid id, StudentSupportService service, CancellationToken ct) =>
            (await service.WithdrawHelpAsync(id, ct)).ToHttp(_ => TypedResults.NoContent()));

        help.MapPost("/{studentId:guid}/resolve", async (
            Guid id, Guid studentId, StudentSupportService service, CancellationToken ct) =>
            (await service.ResolveHelpAsync(id, studentId, ct)).ToHttp(_ => TypedResults.NoContent()));

        return app;
    }
}
