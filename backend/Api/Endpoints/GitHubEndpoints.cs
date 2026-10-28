using Backend.Application.Abstractions;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class GitHubEndpoints
{
    private const string _stateCookieName = "github_oauth_state";
    private const string _userCookieName = "github_oauth_user";
    private const string _redirectCookieName = "github_oauth_redirect";

    public static IEndpointRouteBuilder MapGitHubEndpoints(this IEndpointRouteBuilder app)
    {
        var github = app.MapGroup("/github").WithTags("GitHub");

        // GET /api/github/authorize
        github.MapGet("/authorize", Results<Ok<GitHubAuthorizeDto>, ProblemHttpResult> (
            HttpContext context,
            GitHubLinkingService service,
            ICurrentUser currentUser,
            string? redirect_to = null) =>
        {
            if (!service.IsOAuthConfigured)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: "GitHub:ClientId är inte konfigurerad. Sätt den i user-secrets eller miljövariabler.");
            }

            var state = Guid.NewGuid().ToString("N");
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                MaxAge = TimeSpan.FromMinutes(10)
            };
            context.Response.Cookies.Append(_stateCookieName, state, cookieOptions);
            context.Response.Cookies.Append(_userCookieName, currentUser.UserId.ToString(), cookieOptions);

            if (!string.IsNullOrWhiteSpace(redirect_to))
            {
                context.Response.Cookies.Append(_redirectCookieName, redirect_to, cookieOptions);
            }

            var authorizeUrl = service.BuildAuthorizeUrl(state);
            return TypedResults.Ok(new GitHubAuthorizeDto(authorizeUrl));
        });

        // GET /api/github/callback
        github.MapGet("/callback", async (
            HttpContext context,
            GitHubLinkingService service,
            CancellationToken ct,
            string? code = null,
            string? state = null) =>
        {
            var expectedState = context.Request.Cookies[_stateCookieName];
            var userIdStr = context.Request.Cookies[_userCookieName];
            var redirectTo = context.Request.Cookies[_redirectCookieName] ?? "/settings";

            context.Response.Cookies.Delete(_stateCookieName);
            context.Response.Cookies.Delete(_userCookieName);
            context.Response.Cookies.Delete(_redirectCookieName);

            if (string.IsNullOrWhiteSpace(code)
                || string.IsNullOrWhiteSpace(state)
                || state != expectedState
                || !Guid.TryParse(userIdStr, out var userId))
            {
                return Results.Redirect($"{redirectTo}?github_error=invalid_state");
            }

            var result = await service.LinkForUserAsync(userId, code, ct);
            if (result.Status != Application.Results.ResultStatus.Ok)
            {
                return Results.Redirect($"{redirectTo}?github_error=link_failed");
            }

            return Results.Redirect(redirectTo);
        }).AllowAnonymous();

        // GET /api/github/link
        github.MapGet("/link", async (GitHubLinkingService service, CancellationToken ct) =>
        {
            var login = await service.GetLinkStatusAsync(ct);
            return TypedResults.Ok(new GitHubLinkDto(login));
        });

        // POST /api/github/link
        github.MapPost("/link", async (
            GitHubLinkingService service,
            LinkGitHubRequest request,
            CancellationToken ct) =>
            (await service.LinkAsync(request.Code, ct)).ToHttp(login => TypedResults.Ok(new GitHubLinkDto(login))));

        // DELETE /api/github/link
        github.MapDelete("/link", async (GitHubLinkingService service, CancellationToken ct) =>
            (await service.UnlinkAsync(ct)).ToHttp(_ => TypedResults.NoContent()));

        return app;
    }
}

public record GitHubAuthorizeDto(string Url);

public record GitHubLinkDto(string? Login);

public record LinkGitHubRequest(string Code);
