using Backend.Application.Dtos;
using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class ContentEndpoints
{
    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        // --- content: skapas fristående, publiceras med länkar ---
        var content = app.MapGroup("/content").WithTags("Content");

        content.MapPost("", async (CreateContentDto d, ContentService service, CancellationToken ct) =>
            (await service.CreateAsync(d, ct)).ToHttp(c => Results.Created($"/api/content/{c.Id}", c)));

        content.MapGet("/{id:guid}", async (Guid id, ContentService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } c ? Results.Ok(c) : Results.NotFound());

        content.MapPost("/{id:guid}/links",
            async (Guid id, CreateContentLinkDto d, ContentService service, CancellationToken ct) =>
                (await service.AddLinkAsync(id, d, ct)).ToHttp(l => Results.Created($"/api/content/{id}", l)));

        // --- comments + attachments ---
        content.MapGet("/{id:guid}/comments", async (Guid id, ContentService service, CancellationToken ct) =>
            Results.Ok(await service.GetCommentsAsync(id, ct)));

        content.MapPost("/{id:guid}/comments",
            async (Guid id, CommentDto d, ContentService service, CancellationToken ct) =>
                (await service.AddCommentAsync(id, d, ct)).ToHttp(c => Results.Created($"/api/content/{id}/comments", c)));

        content.MapPost("/{id:guid}/attachments",
            async (Guid id, AttachmentDto d, ContentService service, CancellationToken ct) =>
                (await service.AddAttachmentAsync(id, d, ct)).ToHttp(a => Results.Created($"/api/content/{id}", a)));

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
