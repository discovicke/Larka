namespace Backend.Domain.Entities;

public class AssignmentConfig
{
    public Guid PostId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public int MaxScore { get; set; } = 100;
}
