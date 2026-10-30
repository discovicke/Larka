using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class IntegrationEndpoints
{
    private const string _installStateCookie = "github_install_state";
    private const string _installStatePath = "/api/integrations/github";

    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var integrations = app.MapGroup("/integrations").WithTags("Integrations");

        // Bara skolans admin får se GitHub-konfigurationen. Svaret är sanerat och innehåller inga hemligheter.
        integrations.MapGet("/github", async (IntegrationService service, CancellationToken ct) =>
            (await service.GetGitHubStatusAsync(ct)).ToHttp(status => TypedResults.Ok(status)));

        // Lärarens mallrepon. Bara metadata, aldrig installationstoken.
        integrations.MapGet("/github/templates", async (
            GitHubTemplateService service, CancellationToken ct) =>
            (await service.GetTemplatesAsync(ct)).ToHttp(TypedResults.Ok));

        integrations.MapGet("/github/templates/{owner}/{repo}/latest-commit", async (
            string owner, string repo, GitHubTemplateService service, CancellationToken ct) =>
            (await service.GetLatestCommitAsync(owner, repo, ct)).ToHttp(TypedResults.Ok));

        integrations.MapGet("/github/templates/{owner}/{repo}/tests", async (
            string owner, string repo, GitHubTemplateService service, CancellationToken ct) =>
            (await service.GetTestsAsync(owner, repo, ct)).ToHttp(TypedResults.Ok));

        // Klienten öppnar länken i webbläsaren. State-cookien gör att setup-callbacken kan lita på
        // att det är samma admin och skola som startade installationen.
        integrations.MapGet("/github/install", async (
            HttpContext http,
            IntegrationService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateInstallLinkAsync(ct);
            if (result.Status == ResultStatus.Ok)
            {
                http.Response.Cookies.Append(
                    _installStateCookie,
                    result.Value!.State,
                    InstallCookieOptions(http.Request));
            }

            return result.ToHttp(link => TypedResults.Ok(link));
        });

        // GitHub Appens setup-URL kan peka hit. Endpointen ligger i den skyddade gruppen men måste
        // vara anonym eftersom GitHub inte skickar vårt JWT. Den signerade, HttpOnly-cookien är beviset.
        integrations.MapGet("/github/setup", async (
            HttpContext http,
            IGitHubInstallStateProtector stateProtector,
            IGitHubConnectionService github,
            CancellationToken ct) =>
        {
            var state = http.Request.Cookies[_installStateCookie];
            http.Response.Cookies.Delete(_installStateCookie, InstallCookieOptions(http.Request));

            var installationIdValue = http.Request.Query["installation_id"].ToString();
            if (!long.TryParse(installationIdValue, out var installationId)
                || installationId <= 0
                || !stateProtector.TryValidate(state!, out _, out var schoolId))
            {
                return Results.Redirect(
                    "/settings?github_error="
                    + Uri.EscapeDataString(
                        "GitHub-anslutningen kunde inte verifieras. Försök igen från Inställningar."));
            }

            var status = await github.ConnectAsync(schoolId, installationId, ct);
            return status.Connected
                ? Results.Redirect("/settings?github=connected")
                : Results.Redirect(
                    "/settings?github_error="
                    + Uri.EscapeDataString(status.Error ?? "Kunde inte ansluta GitHub."));
        }).AllowAnonymous();

        integrations.MapPost("/github/connect", async (
            IntegrationService service,
            GitHubConnectRequest request,
            CancellationToken ct) =>
            (await service.ConnectAsync(request, ct)).ToHttp(status => TypedResults.Ok(status)));

        integrations.MapPut("/github/enabled", async (
            IntegrationService service,
            GitHubEnabledRequest request,
            CancellationToken ct) =>
            (await service.SetEnabledAsync(request.Enabled, ct)).ToHttp(status => TypedResults.Ok(status)));

        integrations.MapDelete("/github", async (IntegrationService service, CancellationToken ct) =>
            (await service.DisconnectAsync(ct)).ToHttp(_ => TypedResults.NoContent()));

        return app;
    }

    private static CookieOptions InstallCookieOptions(HttpRequest request) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromMinutes(15),
        Path = _installStatePath
    };
}
