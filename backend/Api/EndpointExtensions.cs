using Backend.Api.Endpoints;

namespace Backend.Api;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthEndpoints();

        var api = app.MapGroup("/api");
        api.MapSchoolEndpoints();
        api.MapUserEndpoints();
        api.MapGroupEndpoints();
        api.MapCourseEndpoints();
        api.MapModuleEndpoints();
        api.MapContentEndpoints();
        api.MapSubmissionEndpoints();

        // Okända /api-anrop ska ge 404, inte klientens index.html.
        api.MapFallback(() => Results.NotFound());

        return app;
    }

    /// <summary>Serverar den byggda klienten från wwwroot. Sökvägar som inte är filer går till index.html.</summary>
    public static IEndpointRouteBuilder MapClient(this IEndpointRouteBuilder app)
    {
        app.MapFallbackToFile("index.html");
        return app;
    }
}
