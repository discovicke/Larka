using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDb db) : IUserRepository
{
    public Task<List<User>> GetAllAsync(CancellationToken ct) =>
        db.Users.OrderBy(user => user.Name).ToListAsync(ct);

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

    public void Add(User user) => db.Users.Add(user);

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
}
