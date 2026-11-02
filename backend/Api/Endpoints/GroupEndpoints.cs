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
        groups.MapGet("/{id:guid}/courses", async Task<Results<Ok<List<CourseDto>>, NotFound>> (
            Guid id, GroupService service, CancellationToken ct) =>
        {
            var courses = await service.GetCoursesAsync(id, ct);
            return courses is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(courses);
        });

        // Lärarna i alla kursernas rader på klassens sida, bara admin.
        groups.MapGet("/{id:guid}/course-teachers", async (Guid id, GroupService service, CancellationToken ct) =>
            (await service.GetCourseTeachersAsync(id, ct)).ToHttp(teachers => TypedResults.Ok(teachers)));

        groups.MapPost("/{id:guid}/courses/{courseId:guid}",
            async Task<Results<Created, Conflict, NotFound<string>, ProblemHttpResult>> (
                Guid id, Guid courseId, GroupService service, CancellationToken ct) =>
            {
                var result = await service.AddCourseAsync(id, courseId, ct);
                if (result.Status == ResultStatus.Forbidden)
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status403Forbidden, detail: result.Error);
                }

                if (result.Status == ResultStatus.NotFound)
                {
                    return TypedResults.NotFound(result.Error!);
                }

                return result.Value
                    ? TypedResults.Created()
                    : TypedResults.Conflict();
            });

        groups.MapGet("/{id:guid}/members", async (Guid id, GroupService service, CancellationToken ct) =>
            (await service.GetMembersAsync(id, ct)).ToHttp(members => TypedResults.Ok(members)));

        // Mentorns vy av en elev: läget i alla klassens kurser. Bara mentorn i klassen och admin, andra får 404.
        groups.MapGet("/{id:guid}/students/{studentId:guid}/overview",
            async (Guid id, Guid studentId, StudentOverviewService service, CancellationToken ct) =>
                (await service.GetAsync(id, studentId, ct)).ToHttp(overview => TypedResults.Ok(overview)));

        groups.MapPost("/{id:guid}/members",
            async (Guid id, AddMemberDto memberDto, GroupService service, CancellationToken ct) =>
                (await service.AddMemberAsync(id, memberDto, ct)).ToHttp(member =>
                    TypedResults.Created($"/api/groups/{id}/members", member)));

        // Klistrad elevlista, bara admin. Svarar med vad som hände med varje rad.
        groups.MapPost("/{id:guid}/members/import",
            async (Guid id, ImportStudentsDto importDto, GroupService service, CancellationToken ct) =>
                (await service.ImportStudentsAsync(id, importDto, ct)).ToHttp(result => TypedResults.Ok(result)));

        groups.MapPut("/{id:guid}",
            async (Guid id, UpdateGroupDto groupDto, GroupService service, CancellationToken ct) =>
                (await service.UpdateAsync(id, groupDto, ct)).ToHttp(group => TypedResults.Ok(group)));

        // 409 om innehåll är publicerat till gruppen.
        groups.MapDelete("/{id:guid}", async (Guid id, GroupService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, ct)).ToHttp(_ => TypedResults.NoContent()));

        // 409 om innehåll i kursen är publicerat till just gruppen.
        groups.MapDelete("/{id:guid}/courses/{courseId:guid}",
            async (Guid id, Guid courseId, GroupService service, CancellationToken ct) =>
                (await service.RemoveCourseAsync(id, courseId, ct)).ToHttp(_ => TypedResults.NoContent()));

        groups.MapPut("/{id:guid}/members/{userId:guid}",
            async (Guid id, Guid userId, UpdateMemberDto memberDto, GroupService service, CancellationToken ct) =>
                (await service.UpdateMemberAsync(id, userId, memberDto, ct))
                    .ToHttp(member => TypedResults.Ok(member)));

        groups.MapDelete("/{id:guid}/members/{userId:guid}",
            async (Guid id, Guid userId, GroupService service, CancellationToken ct) =>
                (await service.RemoveMemberAsync(id, userId, ct)).ToHttp(_ => TypedResults.NoContent()));

        return app;
    }
}
