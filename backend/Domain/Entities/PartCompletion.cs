namespace Backend.Domain.Entities;

// Eleven har klarmarkerat en del (teori eller uppgift utan inlämningssätt).
// Uppgifter med inlämningssätt räknas som klara via Submission i stället.
public class PartCompletion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentLinkId { get; set; }
    public Guid StudentId { get; set; }
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
}
