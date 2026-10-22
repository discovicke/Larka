using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDb _db;

    public UserRepository(AppDb db) => _db = db;

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<List<User>> GetStudentsInGroupAsync(Guid groupId, CancellationToken ct)
    {
        var studentIds = await _db.Enrollments
            .Where(e => e.GroupId == groupId && e.Role == GroupRole.Student)
            .Select(e => e.UserId)
            .ToListAsync(ct);
        return await _db.Users.Where(u => studentIds.Contains(u.Id)).ToListAsync(ct);
    }
}
