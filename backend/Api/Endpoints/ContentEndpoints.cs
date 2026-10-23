using Backend.Application.Dtos;
using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class ContentEndpoints
{
    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        // --- content: skapas fristående, publiceras med länkar ---
        var contentGroup = app.MapGroup("/content").WithTags("Content");

        contentGroup.MapPost("", async (CreateContentDto contentDto, ContentService service, CancellationToken ct) =>
            (await service.CreateAsync(contentDto, ct)).ToHttp(content =>
                Results.Created($"/api/content/{content.Id}", content)));

        contentGroup.MapGet("/{id:guid}", async (Guid id, ContentService service, CancellationToken ct) =>
        {
            var content = await service.GetAsync(id, ct);
            return content is null
                ? Results.NotFound()
                : Results.Ok(content);
        });

        contentGroup.MapPost("/{id:guid}/links",
            async (Guid id, CreateContentLinkDto contentLinkDto, ContentService service, CancellationToken ct) =>
                (await service.AddLinkAsync(id, contentLinkDto, ct)).ToHttp(contentLink =>
                    Results.Created($"/api/content/{id}", contentLink)));

        // --- comments + attachments ---
        contentGroup.MapGet("/{id:guid}/comments", async (Guid id, ContentService service, CancellationToken ct) =>
            Results.Ok(await service.GetCommentsAsync(id, ct)));

        contentGroup.MapPost("/{id:guid}/comments",
            async (Guid id, CommentDto commentDto, ContentService service, CancellationToken ct) =>
                (await service.AddCommentAsync(id, commentDto, ct)).ToHttp(comment =>
                    Results.Created($"/api/content/{id}/comments", comment)));

        contentGroup.MapPost("/{id:guid}/attachments",
            async (Guid id, AttachmentDto attachmentDto, ContentService service, CancellationToken ct) =>
                (await service.AddAttachmentAsync(id, attachmentDto, ct)).ToHttp(attachment =>
                    Results.Created($"/api/content/{id}", attachment)));

        // --- flöden: ?type=news|theory|assignment ---
        app.MapGet("/groups/{id:guid}/content",
                async (Guid id, string? type, ContentService service, CancellationToken ct) =>
                    Results.Ok(await service.GetGroupFeedAsync(id, type, ct)))
            .WithTags("Content");

        app.MapGet("/courses/{id:guid}/content",
                async (Guid id, string? type, ContentService service, CancellationToken ct) =>
                    Results.Ok(await service.GetCourseFeedAsync(id, type, ct)))
            .WithTags("Content");

        return app;
    }
}
