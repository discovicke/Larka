namespace Backend.Domain.Entities;

// Lärarens bedömning av en inlämning. Vid komplettering (status NeedsWork) finns bara feedback, Score är null.
public class Grade
{
    public Guid SubmissionId { get; set; }
    public Guid GradedById { get; set; }
    public int? Score { get; set; }
    public string Feedback { get; set; } = "";
    public DateTimeOffset GradedAt { get; set; } = DateTimeOffset.UtcNow;
}
