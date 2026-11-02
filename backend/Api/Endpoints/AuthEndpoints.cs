using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Inloggningen ligger utanför den skyddade delen av /api, eftersom det är den som ger tokenet.
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", async Task<Results<Ok<LoginResultDto>, ProblemHttpResult>> (
            LoginDto login, AuthService service, CancellationToken ct) =>
        {
            var result = await service.LoginAsync(login, ct);
            return result.Status switch
            {
                ResultStatus.Ok => TypedResults.Ok(result.Value!),
                ResultStatus.Forbidden => TypedResults.Problem(
                    statusCode: StatusCodes.Status403Forbidden, detail: result.Error),
                _ => TypedResults.Problem(
                    statusCode: StatusCodes.Status401Unauthorized, detail: "Fel e-post eller lösenord.")
            };
        });

        // Vilka sätt att logga in som finns. Klienten visar knappen Logga in med Microsoft när Microsoft finns.
        auth.MapGet("/providers", (MicrosoftSignInService service) => TypedResults.Ok(service.GetProviders()));

        // Byter Microsofts ID-token mot Lärkas token. 401 för ett ogiltigt token, 403 när skolan inte har kopplat
        // Microsoft, kontot är spärrat eller adressen redan används, 404 när Microsoft-inloggning inte är inställd.
        auth.MapPost("/microsoft", async Task<Results<Ok<MicrosoftLoginResultDto>, ProblemHttpResult>> (
            MicrosoftLoginDto login, MicrosoftSignInService service, CancellationToken ct) =>
        {
            var result = await service.SignInAsync(login, ct);
            return result.Status switch
            {
                ResultStatus.Ok => TypedResults.Ok(result.Value!),
                ResultStatus.Forbidden => TypedResults.Problem(
                    statusCode: StatusCodes.Status403Forbidden, detail: result.Error),
                ResultStatus.NotFound => TypedResults.Problem(
                    statusCode: StatusCodes.Status404NotFound, detail: "Inloggning med Microsoft är inte inställd."),
                _ => TypedResults.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    detail: "Inloggningen hos Microsoft kunde inte kontrolleras.")
            };
        });

        return app;
    }
}
