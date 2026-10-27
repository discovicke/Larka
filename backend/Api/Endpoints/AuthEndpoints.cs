using Backend.Application.Dtos;
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
            return result is null
                ? TypedResults.Problem(
                    statusCode: StatusCodes.Status401Unauthorized, detail: "Fel e-post eller lösenord.")
                : TypedResults.Ok(result);
        });

        return app;
    }
}
