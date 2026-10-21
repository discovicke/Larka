using Backend.Application.Abstractions;

namespace Backend.Infrastructure.Persistence;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDb _db;

    public EfUnitOfWork(AppDb db) => _db = db;

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
