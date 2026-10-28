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

        return app;
    }
}
