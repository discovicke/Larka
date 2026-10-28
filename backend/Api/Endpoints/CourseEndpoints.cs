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

        courses.MapGet("/{id:guid}/staff", async Task<Results<Ok<List<CourseStaffDto>>, NotFound>> (
            Guid id, CourseService service, CancellationToken ct) =>
        {
            var staff = await service.GetStaffAsync(id, ct);
            return staff is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(staff);
        });

        courses.MapPost("", async (CreateCourseDto courseDto, CourseService service, CancellationToken ct) =>
            (await service.CreateAsync(courseDto, ct)).ToHttp(course =>
                TypedResults.Created($"/api/courses/{course.Id}", course)));

        courses.MapPut("/{id:guid}", async (
            Guid id, UpdateCourseDto courseDto, CourseService service, CancellationToken ct) =>
            (await service.UpdateAsync(id, courseDto, ct)).ToHttp(TypedResults.Ok));

        return app;
    }
}
