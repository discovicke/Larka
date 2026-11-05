using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// Vad som hände, för en användare. Visas i appen; ReadAt markerar den som läst.
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public Guid? ContentId { get; set; }
    public Guid? SubmissionId { get; set; }

    // Kommentaren och den som skrev den, på notiser om kommentarer (CommentMention, CommentReply). Null för andra slag.
    public Guid? CommentId { get; set; }
    public Guid? ActorId { get; set; }

    // Deadlinen en påminnelse (DeadlineSoon) gäller, så att eleven påminns en gång per deadline. Null för andra slag.
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
