using Backend.Domain.Entities;

namespace Backend.Application.Abstractions;

public interface IUserRepository
{
    Task<List<User>> GetAllAsync(CancellationToken ct);
    Task<User?> GetAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<List<User>> GetStudentsInGroupsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct);
    void Add(User user);
}
