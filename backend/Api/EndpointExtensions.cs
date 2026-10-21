using Backend.Api.Endpoints;

namespace Backend.Api;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthEndpoints();
        app.MapGroupEndpoints();
        app.MapPostEndpoints();
        app.MapSubmissionEndpoints();
        app.MapLabEndpoints();
        return app;
    }
}
