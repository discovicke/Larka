using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// En ny kurs som utgår från en tidigare kurs. Bara admin skapar kurser. Beskrivningen följer med, och titel,
// poäng och lärare är källkursens om admin inte anger andra. Gruppen blir läsare, och valda veckor (alla om
// inga anges) kopieras som utkast. Allt sparas i ett enda anrop till databasen, alltså i en transaktion.
// Elever, inlämningar, betyg och framsteg följer aldrig med.
public class CourseRunService(
    ICourseRepository courses,
    IModuleRepository modules,
    IGroupRepository groups,
    IUserRepository users,
    IPermissions permissions,
    ICurrentSchool currentSchool,
    CourseCopyService copyService,
    IUnitOfWork unitOfWork)
{
    public async Task<ServiceResult<CourseRunResultDto>> CreateAsync(
        Guid sourceCourseId, CreateCourseRunDto dto, CancellationToken ct)
    {
        var source = await courses.GetAsync(sourceCourseId, ct);
        if (source is null || !await currentSchool.OwnsAsync(source.SchoolId, ct))
        {
            return ServiceResult<CourseRunResultDto>.NotFound("course not found");
        }

        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<CourseRunResultDto>.Forbidden();
        }

        var code = dto.Code?.Trim() ?? "";
        if (code == "")
        {
            return ServiceResult<CourseRunResultDto>.Invalid("code is required");
        }

        if (dto.StartsOn is null || dto.EndsOn is null)
        {
            return ServiceResult<CourseRunResultDto>.Invalid("startsOn and endsOn are required");
        }

        if (dto.EndsOn.Value < dto.StartsOn.Value)
        {
            return ServiceResult<CourseRunResultDto>.Invalid("endsOn must be on or after startsOn");
        }

        var group = await groups.GetAsync(dto.GroupId, ct);
        if (group is null)
        {
            return ServiceResult<CourseRunResultDto>.NotFound("group not found");
        }

        if (group.SchoolId != source.SchoolId)
        {
            return ServiceResult<CourseRunResultDto>.Invalid("the group belongs to another school");
        }

        if (await courses.CodeExistsAsync(source.SchoolId, code, ct))
        {
            return ServiceResult<CourseRunResultDto>.Invalid("the school already has a course with this code");
        }

        if (dto.Title is not null && dto.Title.Trim() == "")
        {
            return ServiceResult<CourseRunResultDto>.Invalid("title must not be empty");
        }

        var teacherIds = dto.TeacherIds?.Distinct().ToList()
            ?? (await courses.GetTeachersAsync(sourceCourseId, ct)).Select(teacher => teacher.UserId).ToList();
        foreach (var teacherId in teacherIds)
        {
            var user = await users.GetAsync(teacherId, ct);
            if (user is null || user.SchoolId != source.SchoolId || user.Role != UserRole.Teacher)
            {
                return ServiceResult<CourseRunResultDto>.Invalid("teachers must be teachers at the school");
            }
        }

        List<Module> weeks = dto.CopyContent
            ? await modules.GetForCourseAsync(sourceCourseId, ct)
            : [];
        if (dto.CopyContent && dto.ModuleIds is not null)
        {
            weeks = weeks.Where(week => dto.ModuleIds.Contains(week.Id)).ToList();
            if (weeks.Count != dto.ModuleIds.Distinct().Count())
            {
                return ServiceResult<CourseRunResultDto>.Invalid(
                    "moduleIds must only contain weeks in the source course");
            }
        }

        var course = new Course
        {
            SchoolId = source.SchoolId,
            Title = dto.Title?.Trim() ?? source.Title,
            Code = code,
            Description = source.Description,
            Points = dto.Points ?? source.Points,
            StartsOn = dto.StartsOn,
            EndsOn = dto.EndsOn
        };
        courses.Add(course);
        groups.AddCourse(new GroupCourse { GroupId = group.Id, CourseId = course.Id });

        // Lärarna hör till kursen. Klassen får inga lärare, den har bara elever.
        foreach (var teacherId in teacherIds)
        {
            courses.AddTeacher(new CourseTeacher { CourseId = course.Id, UserId = teacherId });
        }

        var plan = new CourseCopyService.CopyPlan();
        try
        {
            if (weeks.Count > 0)
            {
                await copyService.AddCopiesAsync(
                    weeks, course.Id, 1, CourseCopyService.DateShift(source.StartsOn, course.StartsOn), plan, ct);
            }

            await unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            await copyService.DeleteCopiedFilesAsync(plan);
            throw;
        }

        return ServiceResult<CourseRunResultDto>.Ok(
            new CourseRunResultDto(course.Id, group.Id, plan.Modules.Count, plan.PartCount, teacherIds.Count));
    }
}
