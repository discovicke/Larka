using Backend.Application.Abstractions;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backend.Infrastructure.Persistence;

public class EfUnitOfWork(AppDb db) : IUnitOfWork
{
    /// <summary>
    /// Sparar. Om en av våra notiser redan finns (förfrågan och notisjobbet skrev samma notis samtidigt) släpps
    /// den och resten sparas igen, så att ändringen som gav notisen inte misslyckas på grund av en dubblett.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsDuplicateNotification(exception))
        {
            await DropExistingNotificationsAsync(ct);
            await db.SaveChangesAsync(ct);
        }
    }

    private static bool IsDuplicateNotification(DbUpdateException exception)
    {
        var postgres = exception.InnerException as PostgresException;
        return postgres?.SqlState == PostgresErrorCodes.UniqueViolation
            && (postgres.ConstraintName == NotificationConfiguration.OncePerPublishIndex
                || postgres.ConstraintName == NotificationConfiguration.OncePerDeadlineIndex);
    }

    private async Task DropExistingNotificationsAsync(CancellationToken ct)
    {
        var added = db.ChangeTracker.Entries<Notification>()
            .Where(entry => entry.State == EntityState.Added
                && NotificationConfiguration.OnceKeyOf(entry.Entity) is not null)
            .ToList();
        var contentIds = added.Select(entry => entry.Entity.ContentId!.Value).Distinct().ToList();
        var userIds = added.Select(entry => entry.Entity.UserId).Distinct().ToList();
        var existing = (await db.Notifications.AsNoTracking()
                .Where(notification => notification.ContentId != null
                    && contentIds.Contains(notification.ContentId.Value)
                    && userIds.Contains(notification.UserId))
                .ToListAsync(ct))
            .Select(NotificationConfiguration.OnceKeyOf)
            .OfType<OnceKey>()
            .ToHashSet();

        foreach (var entry in added)
        {
            if (existing.Contains(NotificationConfiguration.OnceKeyOf(entry.Entity)!.Value))
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
