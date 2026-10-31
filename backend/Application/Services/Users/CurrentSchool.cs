using Backend.Application.Abstractions;

namespace Backend.Application.Services;

public class CurrentSchool(ICurrentUser currentUser, IUserRepository users) : ICurrentSchool
{
    private Guid? _schoolId;
    private bool _resolved;

    public async Task<Guid?> GetIdAsync(CancellationToken ct)
    {
        if (!_resolved)
        {
            _schoolId = (await users.GetAsync(currentUser.UserId, ct))?.SchoolId;
            _resolved = true;
        }

        return _schoolId;
    }

    public async Task<bool> OwnsAsync(Guid schoolId, CancellationToken ct) =>
        await GetIdAsync(ct) == schoolId;
}
