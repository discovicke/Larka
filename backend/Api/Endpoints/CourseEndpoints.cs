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

        // Lärarna ser vilka elever i kursen som ännu inte har kopplat GitHub.
        courses.MapGet("/{id:guid}/students/unlinked-github", async (
            Guid id, CourseService service, CancellationToken ct) =>
            (await service.GetUnlinkedGitHubStudentsAsync(id, ct)).ToHttp(TypedResults.Ok));

        courses.MapPost("", async (CreateCourseDto courseDto, CourseService service, CancellationToken ct) =>
            (await service.CreateAsync(courseDto, ct)).ToHttp(course =>
                TypedResults.Created($"/api/courses/{course.Id}", course)));

        courses.MapPut("/{id:guid}", async (
            Guid id, UpdateCourseDto courseDto, CourseService service, CancellationToken ct) =>
            (await service.UpdateAsync(id, courseDto, ct)).ToHttp(TypedResults.Ok));

        // Bara admin. 409 om det finns elevarbete i kursen.
        courses.MapDelete("/{id:guid}", async (Guid id, CourseService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, ct)).ToHttp(_ => TypedResults.NoContent()));

        // En ny omgång av kursen för en annan klass. Svarar med den nya kursens id och vad som följde med.
        courses.MapPost("/{id:guid}/runs", async (
            Guid id, CreateCourseRunDto runDto, CourseRunService service, CancellationToken ct) =>
            (await service.CreateAsync(id, runDto, ct)).ToHttp(run =>
                TypedResults.Created($"/api/courses/{run.CourseId}", run)));

        return app;
    }
}
