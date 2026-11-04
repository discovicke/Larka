using Backend.Application.Abstractions;
using Backend.Application.Files;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDb db) : IUserRepository
{
    public Task<List<User>> GetAllAsync(CancellationToken ct) =>
        db.Users.OrderBy(user => user.Name).ToListAsync(ct);

    // Migrationens admin "Placeholder" är ingen riktig person. Så länge ingen kan logga in som hen (inget lösenord och
    // ingen Microsoft-koppling) syns hen inte på Användare och räknas inte bland dem som inte kan logga in (#521).
    public Task<List<User>> GetForSchoolAsync(Guid schoolId, CancellationToken ct) =>
        db.Users
            .Where(user => user.SchoolId == schoolId)
            .Where(user => user.Id != PlaceholderIds.UserId || user.PasswordHash != null || user.ExternalId != null)
            .OrderBy(user => user.Name)
            .ToListAsync(ct);

    public Task<Dictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        db.Users.Where(user => userIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.Name, ct);

    public async Task<Dictionary<Guid, string>> GetAvatarUrlsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        (await db.Users
            .Where(user => userIds.Contains(user.Id) && user.AvatarKey != null)
            .Select(user => new { user.Id, user.AvatarKey })
            .ToListAsync(ct))
        .ToDictionary(user => user.Id, user => AvatarPolicy.UrlFor(user.Id, user.AvatarKey)!);

    public Task<User?> GetAsync(Guid id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(user => user.Id == id, ct);

    public async Task<Dictionary<Guid, List<NotificationKind>>> GetMutedKindsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var rows = await db.Users
            .Where(user => userIds.Contains(user.Id) && user.MutedNotificationKinds.Count > 0)
            .Select(user => new { user.Id, user.MutedNotificationKinds })
            .ToListAsync(ct);
        return rows.ToDictionary(row => row.Id, row => row.MutedNotificationKinds);
    }

    public async Task<(int TotalStudents, int LinkedStudents)> GetGitHubStudentCountsAsync(
        Guid schoolId, CancellationToken ct)
    {
        var students = db.Users.Where(user =>
            user.SchoolId == schoolId && user.Role == UserRole.Student);
        return (
            await students.CountAsync(ct),
            await students.CountAsync(user => user.GitHubLogin != null, ct));
    }

    public void Add(User user) => db.Users.Add(user);

    public Task<User?> GetByExternalIdAsync(string externalId, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(user => user.ExternalId == externalId, ct);

    public Task<bool> HasPasswordAdminAsync(Guid schoolId, CancellationToken ct) =>
        db.Users.AnyAsync(user => user.SchoolId == schoolId
            && user.Role == UserRole.Admin
            && user.PasswordHash != null
            && user.DeactivatedAt == null, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        // E-post jämförs utan hänsyn till versaler och inledande/trailing blanksteg.
        var normalized = email.Trim().ToLowerInvariant();
        return db.Users.FirstOrDefaultAsync(user => user.Email.ToLower() == normalized, ct);
    }

    // En elev som är med i flera av grupperna kommer bara med en gång.
    public async Task<List<User>> GetStudentsInGroupsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct)
    {
        var studentIds = await db.Enrollments
            .Where(enrollment => groupIds.Contains(enrollment.GroupId) && enrollment.Role == GroupRole.Student)
            .Select(enrollment => enrollment.UserId)
            .Distinct()
            .ToListAsync(ct);
        return await db.Users.Where(user => studentIds.Contains(user.Id)).ToListAsync(ct);
    }

    // Som GetStudentsInGroupsAsync, men utan avaktiverade elever. För listor läraren ser, inte för utdelning.
    public async Task<List<User>> GetActiveStudentsInGroupsAsync(
        IReadOnlyCollection<Guid> groupIds, CancellationToken ct)
    {
        var studentIds = await db.Enrollments
            .Where(enrollment => groupIds.Contains(enrollment.GroupId) && enrollment.Role == GroupRole.Student)
            .Select(enrollment => enrollment.UserId)
            .Distinct()
            .ToListAsync(ct);
        return await db.Users
            .Where(user => studentIds.Contains(user.Id) && user.DeactivatedAt == null)
            .ToListAsync(ct);
    }
}
