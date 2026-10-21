namespace Backend.Domain.Entities;

public class Grade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid ByTeacherId { get; set; }
    public int Score { get; set; }
    public string Feedback { get; set; } = "";
}
