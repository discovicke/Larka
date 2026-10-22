using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// Vad som hände, för en användare. Visas i appen; ReadAt markerar den som läst.
// Vart den eventuellt skickas som push styrs av användarens PushSubscriptions.
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public Guid? ContentId { get; set; }
    public Guid? SubmissionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
