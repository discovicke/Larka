using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Notiser och enheter hör till användaren och raderas med den. Raderas innehållet eller inlämningen
// en notis gäller finns notisen kvar, men utan länk (SetNull).

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasOne<User>().WithMany()
            .HasForeignKey(notification => notification.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Content>().WithMany()
            .HasForeignKey(notification => notification.ContentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Submission>().WithMany()
            .HasForeignKey(notification => notification.SubmissionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.HasOne<User>().WithMany()
            .HasForeignKey(subscription => subscription.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(subscription => subscription.Endpoint).IsUnique();
    }
}
