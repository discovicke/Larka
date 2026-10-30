using Backend.Application.Abstractions;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Samlar behörighetsfrågorna på ett ställe. Frågorna mot databasen ligger i respektive repository.
// Undervisning följer relationen (kursens lärare, gruppens lärare), aldrig den globala rollen.
public class PermissionService(
    ICurrentUser currentUser,
    IUserRepository users,
    ICourseRepository courses,
    IGroupRepository groups,
    IContentRepository contents,
    ISubmissionRepository submissions) : IPermissions
{
    public async Task<bool> IsAdminAsync(CancellationToken ct) =>
        (await users.GetAsync(currentUser.UserId, ct))?.Role == UserRole.Admin;

    public Task<bool> CanCreateContentAsync(CancellationToken ct) =>
        groups.TeachesAnythingAsync(currentUser.UserId, ct);

    public Task<bool> CanTeachCourseAsync(Guid courseId, CancellationToken ct) =>
        groups.IsTeacherOfCourseAsync(courseId, currentUser.UserId, ct);

    public Task<bool> CanTeachGroupAsync(Guid groupId, CancellationToken ct) =>
        groups.IsTeacherAsync(groupId, currentUser.UserId, ct);

    public Task<bool> CanTeachLinkAsync(Guid linkId, CancellationToken ct) =>
        submissions.IsTeacherOfLinkAsync(linkId, currentUser.UserId, ct);

    public Task<bool> CanTeachContentAsync(Guid contentId, CancellationToken ct) =>
        contents.IsTeacherOfContentAsync(contentId, currentUser.UserId, ct);

    public async Task<bool> CanManageCourseAsync(Guid courseId, CancellationToken ct)
    {
        var course = await courses.GetAsync(courseId, ct);
        return course is not null && await IsAdminOfSchoolAsync(course.SchoolId, ct);
    }

    public async Task<bool> CanReadCourseAsync(Guid courseId, CancellationToken ct)
    {
        var course = await courses.GetAsync(courseId, ct);
        if (course is null)
        {
            return false;
        }

        return await IsAdminOfSchoolAsync(course.SchoolId, ct)
            || (await groups.GetCourseRolesAsync(currentUser.UserId, ct)).Any(role => role.CourseId == courseId);
    }

    public async Task<bool> CanReadGroupAsync(Guid groupId, CancellationToken ct)
    {
        var group = await groups.GetAsync(groupId, ct);
        if (group is null)
        {
            return false;
        }

        return await IsAdminOfSchoolAsync(group.SchoolId, ct)
            || await groups.IsMemberAsync(groupId, currentUser.UserId, ct)
            || await groups.TeachesCourseOfGroupAsync(groupId, currentUser.UserId, ct);
    }

    public async Task<bool> IsMentorOfStudentAsync(Guid studentId, CancellationToken ct) =>
        (await groups.GetMentoredStudentIdsAsync(currentUser.UserId, ct)).Contains(studentId);

    private async Task<bool> IsAdminOfSchoolAsync(Guid schoolId, CancellationToken ct)
    {
        var user = await users.GetAsync(currentUser.UserId, ct);
        return user is not null && user.Role == UserRole.Admin && user.SchoolId == schoolId;
    }
}
