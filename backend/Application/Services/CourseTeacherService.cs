using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Vem som är lärare i en kurs. Bara admin sätter det, och läraren behöver inte vara medlem i någon klass.
// Läraren ska höra till samma skola som kursen och vara lärare eller admin i systemet, inte elev.
public class CourseTeacherService(
    ICourseRepository courses,
    IUserRepository users,
    IPermissions permissions,
    ICurrentSchool currentSchool,
    IUnitOfWork unitOfWork)
{
    public async Task<ServiceResult<List<CourseTeacherDto>>> GetAsync(Guid courseId, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<List<CourseTeacherDto>>.Forbidden();
        }

        if (await GetOwnAsync(courseId, ct) is null)
        {
            return ServiceResult<List<CourseTeacherDto>>.NotFound("course not found");
        }

        return ServiceResult<List<CourseTeacherDto>>.Ok(
            (await courses.GetTeachersAsync(courseId, ct))
            .Select(teacher => new CourseTeacherDto(teacher.UserId, teacher.Name, teacher.Email))
            .ToList());
    }

    public async Task<ServiceResult<CourseTeacherDto>> AddAsync(
        Guid courseId, AddCourseTeacherDto dto, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<CourseTeacherDto>.Forbidden();
        }

        var course = await GetOwnAsync(courseId, ct);
        if (course is null)
        {
            return ServiceResult<CourseTeacherDto>.NotFound("course not found");
        }

        var user = await users.GetAsync(dto.UserId, ct);
        if (user is null)
        {
            return ServiceResult<CourseTeacherDto>.Invalid("user does not exist");
        }

        if (user.SchoolId != course.SchoolId)
        {
            return ServiceResult<CourseTeacherDto>.Invalid("user belongs to another school");
        }

        if (user.Role == UserRole.Student)
        {
            return ServiceResult<CourseTeacherDto>.Invalid("user must be a teacher or an admin");
        }

        if (await courses.GetTeacherAsync(courseId, user.Id, ct) is not null)
        {
            return ServiceResult<CourseTeacherDto>.Conflict("user is already a teacher of the course");
        }

        courses.AddTeacher(new CourseTeacher { CourseId = courseId, UserId = user.Id });
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<CourseTeacherDto>.Ok(new CourseTeacherDto(user.Id, user.Name, user.Email));
    }

    public async Task<ServiceResult<bool>> RemoveAsync(Guid courseId, Guid userId, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        if (await GetOwnAsync(courseId, ct) is null)
        {
            return ServiceResult<bool>.NotFound("course not found");
        }

        var courseTeacher = await courses.GetTeacherAsync(courseId, userId, ct);
        if (courseTeacher is null)
        {
            return ServiceResult<bool>.NotFound("the user is not a teacher of the course");
        }

        courses.RemoveTeacher(courseTeacher);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    // En kurs i en annan skola räknas som att den inte finns.
    private async Task<Course?> GetOwnAsync(Guid id, CancellationToken ct)
    {
        var course = await courses.GetAsync(id, ct);
        return course is not null && await currentSchool.OwnsAsync(course.SchoolId, ct)
            ? course
            : null;
    }
}
