namespace Backend.Domain.Entities;

// Elevens egen flagga på en uppgift. En rad per elev och utdelning; en löst flagga kan öppnas igen.
public class StudentHelpRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid ContentLinkId { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedById { get; set; }
}
