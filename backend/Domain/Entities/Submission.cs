namespace Backend.Domain.Entities;

public class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public Guid StudentId { get; set; }
    public string BodyMd { get; set; } = "";
    public string? RepoUrl { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public Grade? Grade { get; set; }
}
