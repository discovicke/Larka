using Backend.Application.Dtos;
using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class PostEndpoints
{
    public static IEndpointRouteBuilder MapPostEndpoints(this IEndpointRouteBuilder app)
    {
        // --- posts: ?type=news|theory|assignment ---
        var groupPosts = app.MapGroup("/groups/{id:guid}/posts").WithTags("Posts");

        groupPosts.MapGet("", async (Guid id, string? type, PostService service, CancellationToken ct) =>
            Results.Ok(await service.GetByGroupAsync(id, type, ct)));

        groupPosts.MapPost("", async (Guid id, CreatePostDto d, PostService service, CancellationToken ct) =>
                await service.CreateAsync(id, d, ct) is { } post
                    ? Results.Created($"/posts/{post.Id}", post)
                    : Results.BadRequest("type must be news|theory|assignment"));

        // --- comments + attachments ---
        var posts = app.MapGroup("/posts/{id:guid}").WithTags("Posts");

        posts.MapGet("/comments", async (Guid id, PostService service, CancellationToken ct) =>
            Results.Ok(await service.GetCommentsAsync(id, ct)));

        posts.MapPost("/comments", async (Guid id, CommentDto d, PostService service, CancellationToken ct) =>
        {
            var comment = await service.AddCommentAsync(id, d, ct);
            return Results.Created($"/comments/{comment.Id}", comment);
        });

        posts.MapPost("/attachments", async (Guid id, AttachmentDto d, PostService service, CancellationToken ct) =>
        {
            var attachment = await service.AddAttachmentAsync(id, d, ct);
            return Results.Created($"/attachments/{attachment.Id}", attachment);
        });

        return app;
    }
}
