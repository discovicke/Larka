using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class StudentContentEndpoints
{
    public static IEndpointRouteBuilder MapStudentContentEndpoints(this IEndpointRouteBuilder app)
    {
        // Elevens del av en kurs, i gruppkontext: gruppens egna länkar och kursens gemensamma.
        var groupCourse = app.MapGroup("/groups/{groupId:guid}/courses/{courseId:guid}")
            .WithTags("Student content");

        groupCourse.MapGet("/modules",
            async Task<Results<Ok<List<StudentModuleWithContentDto>>, NotFound<string>>> (
                Guid groupId, Guid courseId, StudentContentService service, CancellationToken ct) =>
            {
                var result = await service.GetForGroupCourseAsync(groupId, courseId, ct);
                return result.Status == ResultStatus.Ok
                    ? TypedResults.Ok(result.Value!)
                    : TypedResults.NotFound(result.Error!);
            });

        groupCourse.MapPut("/links/{linkId:guid}/completion", async (
            Guid groupId, Guid courseId, Guid linkId, SetCompletionDto completionDto,
            StudentContentService service, CancellationToken ct) =>
            (await service.SetCompletionAsync(groupId, courseId, linkId, completionDto, ct))
                .ToHttp(TypedResults.Ok));

        return app;
    }
}
