using Backend.Application.Dtos;
using Backend.Application.Files;
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

        contentGroup.MapGet("/{id:guid}", async Task<Results<Ok<ContentDto>, NotFound>> (
            Guid id, ContentService service, CancellationToken ct) =>
        {
            var content = await service.GetAsync(id, ct);
            return content is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(content);
        });

        contentGroup.MapPut("/{id:guid}", async (
            Guid id, UpdateContentDto contentDto, ContentService service, CancellationToken ct) =>
            (await service.UpdateAsync(id, contentDto, ct)).ToHttp(TypedResults.Ok));

        contentGroup.MapPut("/{id:guid}/status", async (
            Guid id, UpdateContentStatusDto statusDto, ContentService service, CancellationToken ct) =>
            (await service.UpdateStatusAsync(id, statusDto, ct)).ToHttp(TypedResults.Ok));

        contentGroup.MapPost("/{id:guid}/links",
            async (Guid id, CreateContentLinkDto contentLinkDto, ContentService service, CancellationToken ct) =>
                (await service.AddLinkAsync(id, contentLinkDto, ct)).ToHttp(contentLink =>
                    TypedResults.Created($"/api/content/{id}", contentLink)));

        contentGroup.MapPut("/{id:guid}/links/{linkId:guid}", async (
            Guid id, Guid linkId, UpdateContentLinkDto linkDto, ContentService service, CancellationToken ct) =>
            (await service.UpdateLinkAsync(id, linkId, linkDto, ct)).ToHttp(TypedResults.Ok));

        contentGroup.MapPut("/{id:guid}/links/{linkId:guid}/module", async (
            Guid id, Guid linkId, MoveContentLinkDto moveDto, ContentService service, CancellationToken ct) =>
            (await service.MoveLinkAsync(id, linkId, moveDto, ct)).ToHttp(TypedResults.Ok));

        contentGroup.MapDelete("/{id:guid}/links/{linkId:guid}", async (
            Guid id, Guid linkId, ContentService service, CancellationToken ct) =>
            (await service.DeleteLinkAsync(id, linkId, ct)).ToHttp(_ => TypedResults.NoContent()));

        // --- comments + attachments ---
        contentGroup.MapGet("/{id:guid}/comments", async (Guid id, ContentService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetCommentsAsync(id, ct)));

        contentGroup.MapPost("/{id:guid}/comments", async Task<Results<Created<CommentDto>, NotFound>> (
            Guid id, CreateCommentDto commentDto, ContentService service, CancellationToken ct) =>
        {
            var comment = await service.AddCommentAsync(id, commentDto, ct);
            return comment is null
                ? TypedResults.NotFound()
                : TypedResults.Created($"/api/content/{id}/comments", comment);
        });

        // Url i svaret är en kortlivad länk som skapas vid varje anrop.
        contentGroup.MapGet("/{id:guid}/attachments", async Task<Results<Ok<List<AttachmentDto>>, NotFound>> (
            Guid id, ContentService service, CancellationToken ct) =>
        {
            var attachments = await service.GetAttachmentsAsync(id, ct);
            return attachments is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(attachments);
        });

        // En fil per anrop, som multipart/form-data under fältet file.
        contentGroup.MapPost("/{id:guid}/attachments", async (
            Guid id, IFormFile file, ContentService service, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await service.AddAttachmentAsync(id, new FileUpload(file.FileName, file.Length, stream), ct);
            return result.ToHttp(attachment => TypedResults.Created($"/api/content/{id}/attachments", attachment));
        })
            .DisableAntiforgery() // inloggning med cookies finns inte, så inget CSRF-skydd behövs
            .WithMetadata(new RequestSizeLimitAttribute((int)FileUploadPolicy.MaxRequestBytes));

        contentGroup.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", async (
            Guid id, Guid attachmentId, ContentService service, CancellationToken ct) =>
            (await service.DeleteAttachmentAsync(id, attachmentId, ct)).ToHttp(_ => TypedResults.NoContent()));

        // --- flöden: ?type=news|theory|assignment ---
        app.MapGet("/groups/{id:guid}/content",
                async (Guid id, string? type, ContentService service, CancellationToken ct) =>
                    TypedResults.Ok(await service.GetGroupFeedAsync(id, type, ct)))
            .WithTags("Content");

        app.MapGet("/courses/{id:guid}/content",
                async (Guid id, string? type, ContentService service, CancellationToken ct) =>
                    TypedResults.Ok(await service.GetCourseFeedAsync(id, type, ct)))
            .WithTags("Content");

        return app;
    }
}
