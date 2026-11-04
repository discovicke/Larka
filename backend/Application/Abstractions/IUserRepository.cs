using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Abstractions;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync(CancellationToken ct);
    /// <summary>Skolans användare. Migrationens admin "Placeholder" är inte med så länge ingen kan logga in som hen.</summary>
    Task<List<User>> GetForSchoolAsync(Guid schoolId, CancellationToken ct);

    /// <summary>Namnen på användarna, per id. Id:n som inte finns saknas i resultatet.</summary>
    Task<Dictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
    /// <summary>Profilbildernas adresser, per id. Användare utan bild saknas i resultatet.</summary>
    Task<Dictionary<Guid, string>> GetAvatarUrlsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    Task<User?> GetAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);

    /// <summary>Användaren med id:t från Microsoft Entra ID (oid).</summary>
    Task<User?> GetByExternalIdAsync(string externalId, CancellationToken ct);
    Task<List<User>> GetStudentsInGroupsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct);

    /// <summary>
    /// Som GetStudentsInGroupsAsync men utan avaktiverade elever. Används där läraren ser listan. Utdelning och
    /// gruppindelning behåller alla, så att en återaktiverad elev har sina inlämningar kvar.
    /// </summary>
    Task<List<User>> GetActiveStudentsInGroupsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct);

    /// <summary>Avstängda notisslag per användare. Användare utan avstängda slag saknas i resultatet.</summary>
    Task<Dictionary<Guid, List<NotificationKind>>> GetMutedKindsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>Antal elever i skolan och hur många av dem som har kopplat GitHub.</summary>
    Task<(int TotalStudents, int LinkedStudents)> GetGitHubStudentCountsAsync(
        Guid schoolId, CancellationToken ct);

    /// <summary>Om skolan har en aktiv admin som kan logga in med lösenord.</summary>
    Task<bool> HasPasswordAdminAsync(Guid schoolId, CancellationToken ct);
    void Add(User user);
}
