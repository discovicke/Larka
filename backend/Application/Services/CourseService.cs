using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

// En kurs hör till en skola. Vilka grupper som läser den sätts med POST /api/groups/{id}/courses/{courseId}.
// Bara admin får skapa kurser, som för skolor, användare och grupper.
public class CourseService(
    ICourseRepository courses,
    IGroupRepository groups,
    IPermissions permissions,
    ICurrentSchool currentSchool,
    IUnitOfWork unitOfWork)
{
    public async Task<List<CourseDto>> GetAllAsync(CancellationToken ct)
    {
        var schoolId = await currentSchool.GetIdAsync(ct);
        if (schoolId is null)
        {
            return [];
        }

        var schoolCourses = await courses.GetForSchoolAsync(schoolId.Value, ct);
        return schoolCourses.Select(CourseDto.From).ToList();
    }

    public async Task<CourseDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var course = await GetOwnAsync(id, ct);
        return course is null
            ? null
            : CourseDto.From(course);
    }

    public async Task<ServiceResult<CourseDto>> CreateAsync(CreateCourseDto dto, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<CourseDto>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ServiceResult<CourseDto>.Invalid("title is required");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return ServiceResult<CourseDto>.Invalid("code is required");
        }

        // Bara i den egna skolan, annars skulle admin skapa rader som den sedan inte ser.
        if (!await currentSchool.OwnsAsync(dto.SchoolId, ct))
        {
            return ServiceResult<CourseDto>.Invalid("school does not exist");
        }

        var code = dto.Code.Trim();
        if (await courses.CodeExistsAsync(dto.SchoolId, code, ct))
        {
            return ServiceResult<CourseDto>.Invalid("the school already has a course with this code");
        }

        var course = new Course { SchoolId = dto.SchoolId, Title = dto.Title.Trim(), Code = code };
        courses.Add(course);
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<CourseDto>.Ok(CourseDto.From(course));
    }

    /// <summary>Ändrar kursens uppgifter. Koden är låst, eftersom den är unik per skola.</summary>
    public async Task<ServiceResult<CourseDto>> UpdateAsync(Guid id, UpdateCourseDto dto, CancellationToken ct)
    {
        var course = await GetOwnAsync(id, ct);
        if (course is null)
        {
            return ServiceResult<CourseDto>.NotFound("course not found");
        }

        if (!await permissions.CanTeachCourseAsync(id, ct))
        {
            return ServiceResult<CourseDto>.Forbidden();
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return ServiceResult<CourseDto>.Invalid("title is required");
        }

        if (dto.Description is null || dto.Description.Length > 20000)
        {
            return ServiceResult<CourseDto>.Invalid("description can be at most 20000 characters");
        }

        if (dto.Points is not null && (dto.Points < 1 || dto.Points > 400))
        {
            return ServiceResult<CourseDto>.Invalid("points must be between 1 and 400");
        }

        if (dto.StartsOn is not null && dto.EndsOn is not null && dto.EndsOn.Value < dto.StartsOn.Value)
        {
            return ServiceResult<CourseDto>.Invalid("endsOn must be on or after startsOn");
        }

        course.Title = dto.Title.Trim();
        course.Description = dto.Description;
        course.Points = dto.Points;
        course.StartsOn = dto.StartsOn;
        course.EndsOn = dto.EndsOn;
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<CourseDto>.Ok(CourseDto.From(course));
    }

    /// <summary>Lärarna i grupperna som läser kursen. Null om kursen inte finns.</summary>
    public async Task<List<CourseStaffDto>?> GetStaffAsync(Guid id, CancellationToken ct)
    {
        if (await GetOwnAsync(id, ct) is null)
        {
            return null;
        }

        var groupIds = await groups.GetGroupIdsReadingCourseAsync(id, ct);
        var teachers = await groups.GetTeachersAsync(groupIds, ct);
        return teachers
            .GroupBy(teacher => new { teacher.UserId, teacher.Name, teacher.Email })
            .Select(group => new CourseStaffDto(
                group.Key.UserId,
                group.Key.Name,
                group.Key.Email,
                group.Select(teacher => teacher.GroupName).OrderBy(name => name).ToList()))
            .OrderBy(member => member.Name)
            .ToList();
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
