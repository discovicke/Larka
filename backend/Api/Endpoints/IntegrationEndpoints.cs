using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class IntegrationEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var integrations = app.MapGroup("/integrations").WithTags("Integrations");

        // Bara skolans admin får se GitHub-konfigurationen. Svaret är sanerat och innehåller inga hemligheter.
        integrations.MapGet("/github", async (IntegrationService service, CancellationToken ct) =>
            (await service.GetGitHubStatusAsync(ct)).ToHttp(status => TypedResults.Ok(status)));

        integrations.MapPost("/github/connect", async (IntegrationService service, CancellationToken ct) =>
            (await service.ConnectAsync(ct)).ToHttp(status => TypedResults.Ok(status)));

        integrations.MapDelete("/github", async (IntegrationService service, CancellationToken ct) =>
            (await service.DisconnectAsync(ct)).ToHttp(_ => TypedResults.NoContent()));

        return app;
    }
}
