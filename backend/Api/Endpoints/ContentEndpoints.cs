using Backend.Application.Dtos;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

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

        contentGroup.MapGet("/{id:guid}", async Task<Results<Ok<ContentDto>, NotFound>> (
            Guid id, ContentService service, CancellationToken ct) =>
        {
            var content = await service.GetAsync(id, ct);
            return content is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(content);
        });

        contentGroup.MapPost("/{id:guid}/links",
            async (Guid id, CreateContentLinkDto contentLinkDto, ContentService service, CancellationToken ct) =>
                (await service.AddLinkAsync(id, contentLinkDto, ct)).ToHttp(contentLink =>
                    TypedResults.Created($"/api/content/{id}", contentLink)));

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

        // Url i svaret är en kortlivad länk som skapas vid varje anrop. Uppladdning (POST) kommer i #47.
        contentGroup.MapGet("/{id:guid}/attachments", async Task<Results<Ok<List<AttachmentDto>>, NotFound>> (
            Guid id, ContentService service, CancellationToken ct) =>
        {
            var attachments = await service.GetAttachmentsAsync(id, ct);
            return attachments is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(attachments);
        });

        contentGroup.MapDelete("/{id:guid}/attachments/{attachmentId:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id, Guid attachmentId, ContentService service, CancellationToken ct) =>
            await service.DeleteAttachmentAsync(id, attachmentId, ct)
                ? TypedResults.NoContent()
                : TypedResults.NotFound());

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
