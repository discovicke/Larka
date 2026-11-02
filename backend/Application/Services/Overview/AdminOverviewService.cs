using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Application.Results;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Adminens Idag-sida. Visar vad som behöver ordnas i skolan: kurser utan lärare, klasser utan mentor, användare
// som inte kan logga in och elever utan klass. Bara admin, och bara den egna skolan. Spärrade användare räknas inte.
public class AdminOverviewService(
    ICourseRepository courses,
    IGroupRepository groups,
    IUserRepository users,
    ISchoolRepository schools,
    IPermissions permissions,
    ICurrentSchool currentSchool,
    TimeProvider time)
{
    public async Task<ServiceResult<AdminOverviewDto>> GetAsync(CancellationToken ct)
    {
        var schoolId = await currentSchool.GetIdAsync(ct);
        if (schoolId is null || !await permissions.IsAdminAsync(ct))
        {
            return ServiceResult<AdminOverviewDto>.Forbidden();
        }

        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var schoolCourses = await courses.GetForSchoolAsync(schoolId.Value, ct);
        var schoolGroups = (await groups.GetForSchoolAsync(schoolId.Value, ct))
            .OrderBy(group => group.Name)
            .ToList();
        var enrollments = await groups.GetEnrollmentsForSchoolAsync(schoolId.Value, ct);
        var groupCourses = await groups.GetGroupCoursesForSchoolAsync(schoolId.Value, ct);
        var teachedCourseIds = await courses.GetCourseIdsWithTeachersAsync(schoolId.Value, ct);
        var groupsById = schoolGroups.ToDictionary(group => group.Id);

        var withoutTeacher = schoolCourses
            .Where(course => !teachedCourseIds.Contains(course.Id))
            .Select(course => new AdminCourseWithoutTeacherDto(
                course.Id,
                course.Code,
                course.Title,
                groupCourses
                    .Where(groupCourse => groupCourse.CourseId == course.Id
                        && groupsById[groupCourse.GroupId].Type == GroupType.Class)
                    .Select(groupCourse => (Guid?)groupCourse.GroupId)
                    .FirstOrDefault()))
            .ToList();

        var mentoredGroupIds = enrollments
            .Where(enrollment => enrollment.Role == GroupRole.Mentor)
            .Select(enrollment => enrollment.GroupId)
            .ToHashSet();
        var withoutMentor = schoolGroups
            .Where(group => group.Type == GroupType.Class && !mentoredGroupIds.Contains(group.Id))
            .Select(group => new AdminGroupRefDto(group.Id, group.Name, group.Term))
            .ToList();

        var firstClassOfUser = enrollments
            .Where(enrollment => groupsById[enrollment.GroupId].Type == GroupType.Class)
            .GroupBy(enrollment => enrollment.UserId)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => groupsById[grouped.Select(enrollment => enrollment.GroupId)
                    .OrderBy(groupId => groupsById[groupId].Name)
                    .First()]);
        var schoolUsers = (await users.GetForSchoolAsync(schoolId.Value, ct))
            .Where(user => user.DeactivatedAt is null)
            .ToList();
        // Med Microsoft-inloggning kan alla logga in utan lösenord, så då finns ingen sådan lista.
        var school = await schools.GetAsync(schoolId.Value, ct);
        var microsoftLinked = school?.EntraTenantId is not null;
        var withoutPassword = schoolUsers
            .Where(user => !microsoftLinked && string.IsNullOrEmpty(user.PasswordHash))
            .OrderBy(user => user.Name)
            .Select(user => firstClassOfUser.TryGetValue(user.Id, out var group)
                ? new AdminUserWithoutPasswordDto(user.Id, user.Name, group.Id, group.Name)
                : new AdminUserWithoutPasswordDto(user.Id, user.Name, null, null))
            .ToList();

        return ServiceResult<AdminOverviewDto>.Ok(new AdminOverviewDto(
            new AdminCourseCountsDto(
                schoolCourses.Count(course => StateOf(course.StartsOn, course.EndsOn, today) == CourseState.Upcoming),
                schoolCourses.Count(course => StateOf(course.StartsOn, course.EndsOn, today) == CourseState.Running),
                schoolCourses.Count(course => StateOf(course.StartsOn, course.EndsOn, today) == CourseState.Finished),
                schoolCourses.Count(course => StateOf(course.StartsOn, course.EndsOn, today) == CourseState.Undated)),
            withoutTeacher,
            withoutMentor,
            withoutPassword,
            schoolUsers
                .Where(user => user.Role == UserRole.Student && !firstClassOfUser.ContainsKey(user.Id))
                .OrderBy(user => user.Name)
                .Select(user => new AdminUserRefDto(user.Id, user.Name))
                .ToList()));
    }

    private enum CourseState
    {
        Upcoming,
        Running,
        Finished,
        Undated
    }

    // Samma regel som courseStatus.ts i klienten: en kurs pågår till och med slutdagen.
    private static CourseState StateOf(DateOnly? startsOn, DateOnly? endsOn, DateOnly today)
    {
        if (endsOn is not null && endsOn.Value < today)
        {
            return CourseState.Finished;
        }

        if (startsOn is not null && startsOn.Value > today)
        {
            return CourseState.Upcoming;
        }

        return startsOn is not null || endsOn is not null
            ? CourseState.Running
            : CourseState.Undated;
    }
}
