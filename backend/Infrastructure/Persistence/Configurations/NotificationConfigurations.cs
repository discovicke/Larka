using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Notiser och enheter hör till användaren och raderas med den. Raderas innehållet eller inlämningen
// en notis gäller finns notisen kvar, men utan länk (SetNull).

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Content>().WithMany().HasForeignKey(n => n.ContentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Submission>().WithMany().HasForeignKey(n => n.SubmissionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> b)
    {
        b.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(p => p.Endpoint).IsUnique();
    }
}
