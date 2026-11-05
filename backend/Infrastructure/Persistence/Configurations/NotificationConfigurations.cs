using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Notiser och enheter hör till användaren och raderas med den. Raderas innehållet eller inlämningen
// en notis gäller finns notisen kvar, men utan länk (SetNull). En borttagen kommentar tar med sig sina notiser,
// så att ingen får en notis om något som inte finns. Raderas den som skrev kommentaren blir ActorId null.

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
        builder.HasOne<Comment>().WithMany()
            .HasForeignKey(notification => notification.CommentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(notification => notification.ActorId).OnDelete(DeleteBehavior.SetNull);
    }
}
