using Backend.Application.Dtos;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Users");

        users.MapGet("", async (UserService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAllAsync(ct)));

        users.MapGet("/{id:guid}", async Task<Results<Ok<UserDto>, NotFound>> (
            Guid id, UserService service, CancellationToken ct) =>
        {
            var user = await service.GetAsync(id, ct);
            return user is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(user);
        });

        users.MapPost("", async (CreateUserDto userDto, UserService service, CancellationToken ct) =>
            (await service.CreateAsync(userDto, ct)).ToHttp(user =>
                TypedResults.Created($"/api/users/{user.Id}", user)));

        return app;
    }
}
