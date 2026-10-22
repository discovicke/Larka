using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class Enrollment
{
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
    public GroupRole Role { get; set; } = GroupRole.Student;
}
