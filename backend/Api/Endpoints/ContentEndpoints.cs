using Backend.Application.Dtos;
using Backend.Application.Files;
using Backend.Application.Results;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Endpoints;

public static class ContentEndpoints
{
    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        // --- content: skapas fristående, publiceras med länkar ---
        var contentGroup = app.MapGroup("/content").WithTags("Content");

        contentGroup.MapPost("", async (CreateContentDto contentDto, ContentService service, CancellationToken ct) =>
            (await service.CreateAsync(contentDto, ct)).ToHttp(content =>
                TypedResults.Created($"/api/content/{content.Id}", content)));

        contentGroup.MapPost("/with-link", async (
            CreateContentWithLinkDto dto, ContentService service, CancellationToken ct) =>
            (await service.CreateWithLinkAsync(dto, ct)).ToHttp(created =>
                TypedResults.Created($"/api/content/{created.Content.Id}", created)));

        // Läraren ser samma reponamn här som provisioning skapar senare.
        contentGroup.MapPost("/github/repo-name-preview", async (
            RepoNamePreviewRequest dto, ContentService service, CancellationToken ct) =>
            (await service.PreviewRepoNameAsync(dto, ct)).ToHttp(TypedResults.Ok));

        contentGroup.MapGet("/{id:guid}", async Task<Results<Ok<ContentDto>, NotFound>> (
            Guid id, ContentService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(id, ct);
            return result.Status == ResultStatus.Ok
                ? TypedResults.Ok(result.Value!)
                : TypedResults.NotFound();
        });

        contentGroup.MapPut("/{id:guid}", async (
            Guid id, UpdateContentDto contentDto, ContentService service, CancellationToken ct) =>
            (await service.UpdateAsync(id, contentDto, ct)).ToHttp(TypedResults.Ok));

        contentGroup.MapPut("/{id:guid}/status", async (
            Guid id, UpdateContentStatusDto statusDto, ContentService service, CancellationToken ct) =>
            (await service.UpdateStatusAsync(id, statusDto, ct)).ToHttp(TypedResults.Ok));

        contentGroup.MapPost("/{id:guid}/links",
            async (Guid id, CreateContentLinkDto contentLinkDto, ContentLinkService service, CancellationToken ct) =>
                (await service.AddLinkAsync(id, contentLinkDto, ct)).ToHttp(contentLink =>
                    TypedResults.Created($"/api/content/{id}", contentLink)));

        contentGroup.MapPut("/{id:guid}/links/{linkId:guid}", async (
            Guid id, Guid linkId, UpdateContentLinkDto linkDto, ContentLinkService service, CancellationToken ct) =>
            (await service.UpdateLinkAsync(id, linkId, linkDto, ct)).ToHttp(TypedResults.Ok));

        contentGroup.MapPut("/{id:guid}/links/{linkId:guid}/module", async (
            Guid id, Guid linkId, MoveContentLinkDto moveDto, ContentLinkService service, CancellationToken ct) =>
            (await service.MoveLinkAsync(id, linkId, moveDto, ct)).ToHttp(TypedResults.Ok));

        contentGroup.MapDelete("/{id:guid}/links/{linkId:guid}", async (
            Guid id, Guid linkId, ContentLinkService service, CancellationToken ct) =>
            (await service.DeleteLinkAsync(id, linkId, ct)).ToHttp(_ => TypedResults.NoContent()));

        // --- comments + attachments ---
        contentGroup
            .MapGet("/{id:guid}/comments", async (Guid id, ContentCommentService service, CancellationToken ct) =>
                TypedResults.Ok(await service.GetCommentsAsync(id, ct)));

        contentGroup.MapPost("/{id:guid}/comments", async Task<Results<Created<CommentDto>, NotFound>> (
            Guid id, CreateCommentDto commentDto, ContentCommentService service, CancellationToken ct) =>
        {
            var result = await service.AddCommentAsync(id, commentDto, ct);
            return result.Status == ResultStatus.Ok
                ? TypedResults.Created($"/api/content/{id}/comments", result.Value!)
                : TypedResults.NotFound();
        });

        // Url i svaret är en kortlivad länk som skapas vid varje anrop.
        contentGroup.MapGet("/{id:guid}/attachments", async Task<Results<Ok<List<AttachmentDto>>, NotFound>> (
            Guid id, ContentAttachmentService service, CancellationToken ct) =>
        {
            var result = await service.GetAttachmentsAsync(id, ct);
            return result.Status == ResultStatus.Ok
                ? TypedResults.Ok(result.Value!)
                : TypedResults.NotFound();
        });

        // En fil per anrop, som multipart/form-data under fältet file.
        contentGroup.MapPost("/{id:guid}/attachments", async (
            Guid id, IFormFile file, ContentAttachmentService service, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await service.AddAttachmentAsync(id, new FileUpload(file.FileName, file.Length, stream), ct);
            return result.ToHttp(attachment => TypedResults.Created($"/api/content/{id}/attachments", attachment));
        })
            .DisableAntiforgery() // inloggning med cookies finns inte, så inget CSRF-skydd behövs
            .WithMetadata(new RequestSizeLimitAttribute((int)FileUploadPolicy.MaxRequestBytes));

        contentGroup.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", async (
            Guid id, Guid attachmentId, ContentAttachmentService service, CancellationToken ct) =>
            (await service.DeleteAttachmentAsync(id, attachmentId, ct)).ToHttp(_ => TypedResults.NoContent()));

        // Teoridelar som uppgiften hänvisar till. Ligger på utdelningen, inte på innehållet.
        app.MapGet("/links/{id:guid}/related", async (
            Guid id, ContentLinkService service, CancellationToken ct) =>
            (await service.GetRelatedLinksAsync(id, ct)).ToHttp(TypedResults.Ok));

        // Gruppindelningen för en utdelning. Bara lärare på länken får läsa och ändra.
        app.MapGet("/links/{id:guid}/groups", async (
            Guid id, AssignmentGroupService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttp(TypedResults.Ok));

        app.MapPut("/links/{id:guid}/groups", async (
            Guid id, UpdateAssignmentGroupsDto dto, AssignmentGroupService service, CancellationToken ct) =>
            (await service.UpdateAsync(id, dto, ct)).ToHttp(TypedResults.Ok));

        // --- flöden: ?type=news|theory|assignment ---
        app.MapGet("/groups/{id:guid}/content",
                async (Guid id, string? type, ContentService service, CancellationToken ct) =>
                    (await service.GetGroupFeedAsync(id, type, ct)).ToHttp(TypedResults.Ok))
            .WithTags("Content");

        app.MapGet("/courses/{id:guid}/content",
                async (Guid id, string? type, ContentService service, CancellationToken ct) =>
                    (await service.GetCourseFeedAsync(id, type, ct)).ToHttp(TypedResults.Ok))
            .WithTags("Content");

        return app;
    }
}
