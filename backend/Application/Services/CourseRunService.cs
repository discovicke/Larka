using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// En ny omgång av en kurs för en annan klass: kursen med samma titel, beskrivning och poäng, klassen som
// läsare, lärarna som i källkursen och, om man vill, alla veckor. Allt sparas i ett enda anrop till
// databasen, alltså i en transaktion. Elever, inlämningar, betyg och framsteg följer aldrig med.
public class CourseRunService(
    ICourseRepository courses,
    IModuleRepository modules,
    IGroupRepository groups,
    IPermissions permissions,
    CourseCopyService copyService,
    IUnitOfWork unitOfWork)
{
    public async Task<ServiceResult<CourseRunResultDto>> CreateAsync(
        Guid sourceCourseId, CreateCourseRunDto dto, CancellationToken ct)
    {
        var source = await courses.GetAsync(sourceCourseId, ct);
        if (source is null)
        {
            return ServiceResult<CourseRunResultDto>.NotFound("course not found");
        }

        // Admin från vilken kurs som helst, lärare bara från en kurs hen undervisar i.
        if (!await permissions.CanTeachCourseAsync(sourceCourseId, ct))
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

        if (group.Type != GroupType.Class)
        {
            return ServiceResult<CourseRunResultDto>.Invalid("the group must be a class");
        }

        if (await courses.CodeExistsAsync(source.SchoolId, code, ct))
        {
            return ServiceResult<CourseRunResultDto>.Invalid("the school already has a course with this code");
        }

        var course = new Course
        {
            SchoolId = source.SchoolId,
            Title = source.Title,
            Code = code,
            Description = source.Description,
            Points = source.Points,
            StartsOn = dto.StartsOn,
            EndsOn = dto.EndsOn
        };
        courses.Add(course);
        groups.AddCourse(new GroupCourse { GroupId = group.Id, CourseId = course.Id });

        // Kursens lärare följer med till den nya kursen. Klassen får inga lärare, den har bara elever och en mentor.
        var teachers = await courses.GetTeachersAsync(sourceCourseId, ct);
        foreach (var teacher in teachers)
        {
            courses.AddTeacher(new CourseTeacher { CourseId = course.Id, UserId = teacher.UserId });
        }

        var staffCount = teachers.Count;

        var plan = new CourseCopyService.CopyPlan();
        try
        {
            if (dto.CopyContent)
            {
                var weeks = await modules.GetForCourseAsync(sourceCourseId, ct);
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
            new CourseRunResultDto(course.Id, group.Id, plan.Modules.Count, plan.PartCount, staffCount));
    }
}
