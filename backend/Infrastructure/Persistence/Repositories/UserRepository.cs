using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDb db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(user => user.Email == email, ct);

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
