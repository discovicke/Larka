using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var groups = app.MapGroup("/groups").WithTags("Groups");

        groups.MapGet("", async (GroupService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAllAsync(ct)));

        groups.MapGet("/{id:guid}", async Task<Results<Ok<GroupDto>, NotFound>> (
            Guid id, GroupService service, CancellationToken ct) =>
        {
            var group = await service.GetAsync(id, ct);
            return group is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(group);
        });

        groups.MapPost("", async (CreateGroupDto groupDto, GroupService service, CancellationToken ct) =>
            (await service.CreateAsync(groupDto, ct)).ToHttp(group =>
                TypedResults.Created($"/api/groups/{group.Id}", group)));

        // Vilka kurser läser en grupp? Tom lista = fristående grupp (t.ex. committee).
        groups.MapGet("/{id:guid}/courses", async (Guid id, GroupService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetCoursesAsync(id, ct)));

        groups.MapPost("/{id:guid}/courses/{courseId:guid}", async Task<Results<Created, Conflict, NotFound<string>>> (
            Guid id, Guid courseId, GroupService service, CancellationToken ct) =>
        {
            var result = await service.AddCourseAsync(id, courseId, ct);
            if (result.Status == ResultStatus.NotFound)
            {
                return TypedResults.NotFound(result.Error!);
            }

            return result.Value
                ? TypedResults.Created()
                : TypedResults.Conflict();
        });

        groups.MapPost("/{id:guid}/members",
            async (Guid id, AddMemberDto memberDto, GroupService service, CancellationToken ct) =>
                (await service.AddMemberAsync(id, memberDto, ct)).ToHttp(member =>
                    TypedResults.Created($"/api/groups/{id}/members", member)));

        return app;
    }
}
