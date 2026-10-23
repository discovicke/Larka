using Backend.Application.Abstractions;

namespace Backend.Infrastructure.Persistence;

public class EfUnitOfWork(AppDb db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
