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
    IUserRepository users,
    IModuleRepository modules,
    IContentRepository contents,
    ISubmissionRepository submissions,
    IPartCompletionRepository completions,
    IPermissions permissions,
    ICurrentUser currentUser,
    ICurrentSchool currentSchool,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    ILogger<CourseService> logger)
{
    public async Task<List<CourseDto>> GetAllAsync(CancellationToken ct)
    {
        var schoolId = await currentSchool.GetIdAsync(ct);
        if (schoolId is null)
        {
            return [];
        }

        // Admin ser hela skolan. Andra ser kurserna som deras grupper läser, och kurserna de är lärare i.
        var schoolCourses = await permissions.IsAdminAsync(ct)
            ? await courses.GetForSchoolAsync(schoolId.Value, ct)
            : await courses.GetForMemberAsync(schoolId.Value, currentUser.UserId, ct);
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

        var rangeError = PointsAndDatesError(dto.Points, dto.StartsOn, dto.EndsOn);
        if (rangeError is not null)
        {
            return ServiceResult<CourseDto>.Invalid(rangeError);
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

        var course = new Course
        {
            SchoolId = dto.SchoolId,
            Title = dto.Title.Trim(),
            Code = code,
            Points = dto.Points,
            StartsOn = dto.StartsOn,
            EndsOn = dto.EndsOn,
        };
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

        var rangeError = PointsAndDatesError(dto.Points, dto.StartsOn, dto.EndsOn);
        if (rangeError is not null)
        {
            return ServiceResult<CourseDto>.Invalid(rangeError);
        }

        course.Title = dto.Title.Trim();
        course.Description = dto.Description;
        course.Points = dto.Points;
        course.StartsOn = dto.StartsOn;
        course.EndsOn = dto.EndsOn;
        await unitOfWork.SaveChangesAsync(ct);
        return ServiceResult<CourseDto>.Ok(CourseDto.From(course));
    }

    /// <summary>
    /// Tar bort kursen med veckorna, innehållet som bara hör till den, lärarna och kopplingarna till grupperna.
    /// Elevarbete stoppar borttagningen (409), så att inget försvinner av misstag. Utdelningar utan
    /// elevaktivitet följer med.
    /// </summary>
    public async Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        if (!await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<bool>.Forbidden();
        }

        var course = await GetOwnAsync(id, ct);
        if (course is null)
        {
            return ServiceResult<bool>.NotFound("course not found");
        }

        var parts = await contents.GetLinksWithContentForCourseAsync(id, ct);
        foreach (var part in parts)
        {
            var linkSubmissions = await submissions.GetByLinkAsync(part.Link.Id, ct);
            if (linkSubmissions.Any(StudentWorkPolicy.HasStudentActivity)
                || await completions.AnyForLinkAsync(part.Link.Id, ct))
            {
                return ServiceResult<bool>.Conflict(
                    $"the course has student work in \"{part.Content.Title}\" and cannot be deleted");
            }
        }

        // Innehåll som också är publicerat någon annanstans lever vidare, resten följer med kursen.
        var contentToDelete = new List<Content>();
        var attachments = new List<Attachment>();
        foreach (var group in parts.GroupBy(part => part.Content.Id))
        {
            if (await contents.CountLinksAsync(group.Key, ct) != group.Count())
            {
                continue;
            }

            contentToDelete.Add(group.First().Content);
            attachments.AddRange(await contents.GetAttachmentsAsync(group.Key, ct));
        }

        foreach (var part in parts)
        {
            // Aktivitet har redan stoppat borttagningen, så det som är kvar är tomma utdelningsrader.
            foreach (var submission in await submissions.GetByLinkAsync(part.Link.Id, ct))
            {
                submissions.Remove(submission);
            }

            contents.RemoveLink(part.Link);
        }

        foreach (var content in contentToDelete)
        {
            contents.RemoveContent(content);
        }

        foreach (var module in await modules.GetForCourseAsync(id, ct))
        {
            modules.Remove(module);
        }

        foreach (var groupId in await groups.GetGroupIdsReadingCourseAsync(id, ct))
        {
            await groups.RemoveCourseAsync(groupId, id, ct);
        }

        await courses.RemoveTeachersAsync(id, ct);

        courses.Remove(course);
        await unitOfWork.SaveChangesAsync(ct);
        foreach (var attachment in attachments)
        {
            await DeleteStorageFileQuietlyAsync(attachment.StorageKey);
        }

        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>Kursens lärare, med namnen på grupperna som läser kursen. Null om kursen inte finns.</summary>
    public async Task<List<CourseStaffDto>?> GetStaffAsync(Guid id, CancellationToken ct)
    {
        if (await GetOwnAsync(id, ct) is null)
        {
            return null;
        }

        var groupNames = await groups.GetGroupNamesReadingCourseAsync(id, ct);
        return (await courses.GetTeachersAsync(id, ct))
            .Select(teacher => new CourseStaffDto(teacher.UserId, teacher.Name, teacher.Email, groupNames))
            .ToList();
    }

    /// <summary>
    /// Elever som läser kursen men saknar GitHubLogin. Lärarna använder listan för att påminna dem
    /// innan en repo-uppgift delas ut.
    /// </summary>
    public async Task<ServiceResult<List<UnlinkedGitHubStudentDto>>> GetUnlinkedGitHubStudentsAsync(
        Guid courseId, CancellationToken ct)
    {
        if (!await groups.CourseExistsAsync(courseId, ct))
        {
            return ServiceResult<List<UnlinkedGitHubStudentDto>>.NotFound("course not found");
        }

        if (!await permissions.CanTeachCourseAsync(courseId, ct))
        {
            return ServiceResult<List<UnlinkedGitHubStudentDto>>.Forbidden();
        }

        var groupIds = await groups.GetGroupIdsReadingCourseAsync(courseId, ct);
        var students = await users.GetStudentsInGroupsAsync(groupIds, ct);
        return ServiceResult<List<UnlinkedGitHubStudentDto>>.Ok(
            students
                .Where(student => string.IsNullOrWhiteSpace(student.GitHubLogin))
                .Select(student => new UnlinkedGitHubStudentDto(
                    student.Id, student.Name, student.Email))
                .OrderBy(student => student.Name)
                .ToList());
    }

    // En kurs i en annan skola räknas som att den inte finns.
    private async Task<Course?> GetOwnAsync(Guid id, CancellationToken ct)
    {
        var course = await courses.GetAsync(id, ct);
        return course is not null && await currentSchool.OwnsAsync(course.SchoolId, ct)
            ? course
            : null;
    }

    private static string? PointsAndDatesError(int? points, DateOnly? startsOn, DateOnly? endsOn)
    {
        if (points is not null && (points < 1 || points > 400))
        {
            return "points must be between 1 and 400";
        }

        return startsOn is not null && endsOn is not null && endsOn.Value < startsOn.Value
            ? "endsOn must be on or after startsOn"
            : null;
    }

    private async Task DeleteStorageFileQuietlyAsync(string key)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not delete {StorageKey} from file storage", key);
        }
    }
}
