using Backend.Application.Abstractions;
using Backend.Application.Dtos;

namespace Backend.Application.Services;

// GET /api/me: vem användaren är och vilken roll hen har per kurs. Bara riktiga roller: admin är inte lärare i en
// kurs förrän hen står i CourseTeachers, som alla andra.
public class MeService(
    IUserRepository users,
    ISchoolRepository schools,
    IGroupRepository groups,
    ISchoolGitHubInstallationRepository installations,
    IUnitOfWork unitOfWork,
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

        var courseRoles = (await groups.GetCourseRolesAsync(user.Id, ct))
            .Select(courseRole => new CourseRoleDto(courseRole.CourseId, courseRole.Role))
            .ToList();

        var github = await installations.GetForSchoolAsync(user.SchoolId, ct);
        return new MeDto(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            new SchoolRefDto(school.Id, school.Name),
            courseRoles,
            (await groups.GetMemberGroupsAsync(user.Id, ct))
                .Select(group => new GroupRefDto(group.Id, group.Name))
                .ToList(),
            user.GitHubLogin,
            github?.Connected == true && github.Enabled);
    }

    /// <summary>Slagen av notiser användaren stängt av. Null om användaren inte finns.</summary>
    public async Task<NotificationSettingsDto?> GetNotificationSettingsAsync(CancellationToken ct)
    {
        var user = await users.GetAsync(currentUser.UserId, ct);
        return user is null
            ? null
            : new NotificationSettingsDto(user.MutedNotificationKinds);
    }

    /// <summary>Sparar vilka slag som är avstängda. Dubbletter tas bort. Null om användaren inte finns.</summary>
    public async Task<NotificationSettingsDto?> SaveNotificationSettingsAsync(
        NotificationSettingsDto settings, CancellationToken ct)
    {
        var user = await users.GetAsync(currentUser.UserId, ct);
        if (user is null)
        {
            return null;
        }

        user.MutedNotificationKinds = settings.Muted.Distinct().Order().ToList();
        await unitOfWork.SaveChangesAsync(ct);
        return new NotificationSettingsDto(user.MutedNotificationKinds);
    }
}
