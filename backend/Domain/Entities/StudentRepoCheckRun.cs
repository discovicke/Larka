namespace Backend.Domain.Entities;

// Senaste kända check runs för elevens repo. Används för att visa kravstatus utan att fråga GitHub
// vid varje sidladdning.
public class StudentRepoCheckRun
{
    public Guid SubmissionId { get; set; }
    public string ExternalKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Conclusion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
