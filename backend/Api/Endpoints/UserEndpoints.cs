using Backend.Application.Dtos;
using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Users");

        // Bara admin, och bara skolans egna användare.
        users.MapGet("", async (UserService service, CancellationToken ct) =>
            (await service.GetAllAsync(ct)).ToHttp(list => TypedResults.Ok(list)));

        users.MapGet("/{id:guid}", async (Guid id, UserService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttp(user => TypedResults.Ok(user)));

        users.MapPost("", async (CreateUserDto userDto, UserService service, CancellationToken ct) =>
            (await service.CreateAsync(userDto, ct)).ToHttp(user =>
                TypedResults.Created($"/api/users/{user.Id}", user)));

        // Bara admin, och inte den egna rollen (409).
        users.MapPut("/{id:guid}/role",
            async (Guid id, UpdateUserRoleDto roleDto, UserService service, CancellationToken ct) =>
                (await service.UpdateRoleAsync(id, roleDto, ct)).ToHttp(user => TypedResults.Ok(user)));

        // Spärrar och återaktiverar. Bara admin, och inte sig själv (409).
        users.MapPost("/{id:guid}/deactivate", async (Guid id, UserService service, CancellationToken ct) =>
            (await service.DeactivateAsync(id, ct)).ToHttp(user => TypedResults.Ok(user)));

        users.MapPost("/{id:guid}/reactivate", async (Guid id, UserService service, CancellationToken ct) =>
            (await service.ReactivateAsync(id, ct)).ToHttp(user => TypedResults.Ok(user)));

        users.MapDelete("/{id:guid}/microsoft", async (Guid id, UserService service, CancellationToken ct) =>
            (await service.UnlinkMicrosoftAsync(id, ct)).ToHttp(user => TypedResults.Ok(user)));

        return app;
    }
}
