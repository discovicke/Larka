using Backend.Application.Dtos;
using Backend.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Backend.Api.Endpoints;

public static class CourseEndpoints
{
    public static IEndpointRouteBuilder MapCourseEndpoints(this IEndpointRouteBuilder app)
    {
        var courses = app.MapGroup("/courses").WithTags("Courses");

        courses.MapGet("", async (CourseService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAllAsync(ct)));

        courses.MapGet("/{id:guid}", async Task<Results<Ok<CourseDto>, NotFound>> (
            Guid id, CourseService service, CancellationToken ct) =>
        {
            var course = await service.GetAsync(id, ct);
            return course is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(course);
        });

        courses.MapPost("", async (CreateCourseDto courseDto, CourseService service, CancellationToken ct) =>
            (await service.CreateAsync(courseDto, ct)).ToHttp(course =>
                TypedResults.Created($"/api/courses/{course.Id}", course)));

        return app;
    }
}
