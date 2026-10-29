using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Abstractions;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync(CancellationToken ct);
    Task<User?> GetAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<List<User>> GetStudentsInGroupsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct);

    /// <summary>Avstängda notisslag per användare. Användare utan avstängda slag saknas i resultatet.</summary>
    Task<Dictionary<Guid, List<NotificationKind>>> GetMutedKindsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>Antal elever i skolan och hur många av dem som har kopplat GitHub.</summary>
    Task<(int TotalStudents, int LinkedStudents)> GetGitHubStudentCountsAsync(
        Guid schoolId, CancellationToken ct);
    void Add(User user);
}
