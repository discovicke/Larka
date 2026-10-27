using Backend.Application.Abstractions;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Samlar behörighetsfrågorna på ett ställe. Frågorna mot databasen ligger i respektive repository.
public class PermissionService(
    ICurrentUser currentUser,
    IUserRepository users,
    IGroupRepository groups,
    IContentRepository contents,
    ISubmissionRepository submissions) : IPermissions
{
    public async Task<bool> IsAdminAsync(CancellationToken ct) =>
        (await users.GetAsync(currentUser.UserId, ct))?.Role == UserRole.Admin;

    public async Task<bool> CanCreateContentAsync(CancellationToken ct)
    {
        var user = await users.GetAsync(currentUser.UserId, ct);
        return user?.Role is UserRole.Teacher or UserRole.Admin;
    }

    public async Task<bool> CanTeachCourseAsync(Guid courseId, CancellationToken ct) =>
        await IsAdminAsync(ct) || await groups.IsTeacherOfCourseAsync(courseId, currentUser.UserId, ct);

    public async Task<bool> CanTeachGroupAsync(Guid groupId, CancellationToken ct) =>
        await IsAdminAsync(ct) || await groups.IsTeacherAsync(groupId, currentUser.UserId, ct);

    public async Task<bool> CanTeachLinkAsync(Guid linkId, CancellationToken ct) =>
        await IsAdminAsync(ct) || await submissions.IsTeacherOfLinkAsync(linkId, currentUser.UserId, ct);

    public async Task<bool> CanTeachContentAsync(Guid contentId, CancellationToken ct) =>
        await IsAdminAsync(ct) || await contents.IsTeacherOfContentAsync(contentId, currentUser.UserId, ct);
}
