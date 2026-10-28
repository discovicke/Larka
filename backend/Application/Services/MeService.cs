using Backend.Application.Abstractions;
using Backend.Application.Dtos;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// GET /api/me: vem användaren är och vilken roll hen har per kurs. Admin får alla skolans kurser som Teacher.
public class MeService(
    IUserRepository users,
    ISchoolRepository schools,
    ICourseRepository courses,
    IGroupRepository groups,
    ICurrentUser currentUser)
{
    /// <summary>Returnerar null om den inloggade användaren inte finns.</summary>
    public async Task<MeDto?> GetAsync(CancellationToken ct)
    {
        var user = await users.GetAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return null;
        }

        var school = await schools.GetAsync(user.SchoolId, ct);
        if (school is null)
        {
            return null;
        }

        var courseRoles = user.Role == UserRole.Admin
            ? (await courses.GetForSchoolAsync(user.SchoolId, ct))
                .Select(course => new CourseRoleDto(course.Id, GroupRole.Teacher))
                .ToList()
            : (await groups.GetCourseRolesAsync(user.Id, ct))
                .Select(courseRole => new CourseRoleDto(courseRole.CourseId, courseRole.Role))
                .ToList();

        return new MeDto(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            new SchoolRefDto(school.Id, school.Name),
            courseRoles,
            user.GitHubLogin);
    }
}
