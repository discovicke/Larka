using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

// En rad per elev och utdelning (ContentLink), eftersom deadlinen ligger på länken.
// Skapas med status NotStarted när uppgiften delas ut, eller när eleven lämnar in första gången.
public class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentLinkId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? AssignmentGroupId { get; set; }
    public AssignmentGroup? Group { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.NotStarted;
    public string? BodyMd { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public List<SubmissionFile> Files { get; set; } = [];
    public StudentRepo? Repo { get; set; }
    public Grade? Grade { get; set; }
}
