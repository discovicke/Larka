using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

public class ContentAccess(
    IPermissions permissions,
    IUserRepository users,
    ICurrentSchool currentSchool,
    ICurrentUser currentUser)
{
    // Lärare på någon av innehållets länkar, eller den som skrev innehållet. Admin har ingen genväg hit.
    public async Task<bool> MayEditAsync(Content content, CancellationToken ct) =>
        content.AuthorId == currentUser.UserId || await permissions.CanTeachContentAsync(content.Id, ct);

    // Publicerat innehåll läses av alla inloggade, ett utkast bara av dem som får ändra det och av admin i skolan.
    public async Task<bool> MayReadAsync(Content content, CancellationToken ct)
    {
        if (content.Status != ContentStatus.Draft || await MayEditAsync(content, ct))
        {
            return true;
        }

        var author = await users.GetAsync(content.AuthorId, ct);
        return author is not null
            && await permissions.IsAdminAsync(ct)
            && await currentSchool.OwnsAsync(author.SchoolId, ct);
    }
}
