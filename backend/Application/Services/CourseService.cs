using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

// En kurs hör till en skola. Vilka grupper som läser den sätts med POST /api/groups/{id}/courses/{courseId}.
public class CourseService(ICourseRepository courses, ISchoolRepository schools, IUnitOfWork unitOfWork)
{
    public async Task<List<CourseDto>> GetAllAsync(CancellationToken ct)
    {
        var allCourses = await courses.GetAllAsync(ct);
        return allCourses.Select(CourseDto.From).ToList();
    }

    public async Task<CourseDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var course = await courses.GetAsync(id, ct);
        return course is null
            ? null
            : CourseDto.From(course);
    }

    public async Task<ValidationResult<CourseDto>> CreateAsync(CreateCourseDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ValidationResult<CourseDto>.Invalid("title is required");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return ValidationResult<CourseDto>.Invalid("code is required");
        }

        if (!await schools.ExistsAsync(dto.SchoolId, ct))
        {
            return ValidationResult<CourseDto>.Invalid("school does not exist");
        }

        var code = dto.Code.Trim();
        if (await courses.CodeExistsAsync(dto.SchoolId, code, ct))
        {
            return ValidationResult<CourseDto>.Invalid("the school already has a course with this code");
        }

        var course = new Course { SchoolId = dto.SchoolId, Title = dto.Title.Trim(), Code = code };
        courses.Add(course);
        await unitOfWork.SaveChangesAsync(ct);
        return ValidationResult<CourseDto>.Ok(CourseDto.From(course));
    }
}
