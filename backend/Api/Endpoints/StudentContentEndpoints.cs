using Backend.Application.Dtos;
using Backend.Application.Services;

namespace Backend.Api.Endpoints;

public static class StudentContentEndpoints
{
    public static IEndpointRouteBuilder MapStudentContentEndpoints(this IEndpointRouteBuilder app)
    {
        // Den inloggade elevens del av en kurs: egna gruppers länkar och kursens gemensamma.
        // Grupperna slås upp på servern, så en elev kan inte läsa en annan grupps vy.
        var course = app.MapGroup("/courses/{courseId:guid}")
            .WithTags("Student content");

        course.MapGet("/student-modules", async (
            Guid courseId, StudentContentService service, CancellationToken ct) =>
            (await service.GetForCurrentUserAsync(courseId, ct)).ToHttp(TypedResults.Ok));

        course.MapPut("/links/{linkId:guid}/completion", async (
            Guid courseId, Guid linkId, SetCompletionDto completionDto,
            StudentContentService service, CancellationToken ct) =>
            (await service.SetCompletionAsync(courseId, linkId, completionDto, ct))
                .ToHttp(TypedResults.Ok));

        course.MapGet("/links/{linkId:guid}/checklist", async (
            Guid courseId, Guid linkId, StudentContentService service, CancellationToken ct) =>
            (await service.GetChecklistAsync(courseId, linkId, ct)).ToHttp(TypedResults.Ok));

        course.MapPut("/links/{linkId:guid}/checklist/{index:int}", async (
            Guid courseId, Guid linkId, int index, SetChecklistTickDto tickDto,
            StudentContentService service, CancellationToken ct) =>
            (await service.SetChecklistTickAsync(courseId, linkId, index, tickDto, ct))
                .ToHttp(TypedResults.Ok));

        return app;
    }
}
