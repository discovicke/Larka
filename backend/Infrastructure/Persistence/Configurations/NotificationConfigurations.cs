using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Notiser och enheter hör till användaren och raderas med den. Raderas innehållet eller inlämningen
// en notis gäller finns notisen kvar, men utan länk (SetNull). En borttagen kommentar tar med sig sina notiser,
// så att ingen får en notis om något som inte finns. Raderas den som skrev kommentaren blir ActorId null.

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public const string OncePerPublishIndex = "IX_Notifications_OncePerPublish";
    public const string OncePerDeadlineIndex = "IX_Notifications_OncePerDeadline";

    /// <summary>
    /// Det de unika indexen jämför: en notis per användare och nyhet eller uppgift, och en påminnelse per användare,
    /// innehåll och deadline. Null för notiser som får finnas flera gånger.
    /// </summary>
    public static OnceKey? OnceKeyOf(Notification notification)
    {
        if (notification.ContentId is null)
        {
            return null;
        }

        var contentId = notification.ContentId.Value;
        if (notification.Kind is NotificationKind.NewsPublished or NotificationKind.AssignmentPublished)
        {
            return new OnceKey(notification.Kind, contentId, notification.UserId, null);
        }

        if (notification.Kind == NotificationKind.DeadlineSoon && notification.DueAt is not null)
        {
            return new OnceKey(notification.Kind, contentId, notification.UserId, notification.DueAt.Value.UtcTicks);
        }

        return null;
    }

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

        // Förfrågan som publicerar och notisjobbet kollar båda om notisen redan finns innan de skriver. Indexen
        // stoppar en dubblett om de gör det samtidigt, eller om två instanser kör jobbet under en deploy.
        // Kind står först, så att EF behåller sina egna index på UserId och ContentId.
        builder.HasIndex(
                notification => new { notification.Kind, notification.ContentId, notification.UserId },
                OncePerPublishIndex)
            .IsUnique()
            .HasFilter("\"Kind\" IN ('NewsPublished', 'AssignmentPublished')");
        builder.HasIndex(
                notification => new
                {
                    notification.Kind,
                    notification.ContentId,
                    notification.UserId,
                    notification.DueAt
                },
                OncePerDeadlineIndex)
            .IsUnique()
            .HasFilter("\"Kind\" = 'DeadlineSoon'");
    }
}

public readonly record struct OnceKey(NotificationKind Kind, Guid ContentId, Guid UserId, long? DueAtTicks);
