using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notifications").WithTags("Notifications");

        // Den inloggades notiser, de nyaste först (högst limit, som standard 50), och antalet olästa.
        group.MapGet("/", async (int? limit, NotificationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(limit, ct)));

        group.MapGet("/unread-count", async (NotificationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetUnreadCountAsync(ct)));

        group.MapPost("/read-all", async (NotificationService service, CancellationToken ct) =>
        {
            await service.MarkAllReadAsync(ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/{id:guid}/read", async (Guid id, NotificationService service, CancellationToken ct) =>
            (await service.MarkReadAsync(id, ct)).ToHttp(_ => TypedResults.NoContent()));

        return app;
    }
}
