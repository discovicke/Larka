using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class StudentLabRepo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LabId { get; set; }
    public Guid StudentId { get; set; }
    public string RepoFullName { get; set; } = "";
    public string? LastSha { get; set; }
    public CheckStatus CheckStatus { get; set; } = CheckStatus.Pending;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
