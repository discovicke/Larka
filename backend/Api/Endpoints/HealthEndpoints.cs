using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Backend.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = (context, report) =>
            {
                context.Response.ContentType = "text/plain";
                return context.Response.WriteAsync(
                    report.Status == HealthStatus.Healthy ? "OK" : report.Status.ToString());
            }
        }).WithTags("Health");

        // Visar vilken version och commit som körs, så att deployen kan kontrollera att rätt kod är live.
        // APP_VERSION och GIT_COMMIT bakas in i Docker-imagen när cd.yml bygger den.
        app.MapGet("/version", (IConfiguration config) => new VersionResponse(
            config["APP_VERSION"] ?? "dev",
            config["GIT_COMMIT"] ?? "local")).WithTags("Health");

        return app;
    }
}

public record VersionResponse(string Version, string Commit);
