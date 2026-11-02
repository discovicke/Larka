namespace Backend.Domain.Entities;

public class AssignmentGroupMember
{
    public Guid AssignmentGroupId { get; set; }
    public Guid StudentId { get; set; }
    public User? User { get; set; }
}
