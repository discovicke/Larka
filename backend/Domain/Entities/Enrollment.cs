namespace Backend.Domain.Entities;

public class Enrollment
{
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
    public string RoleInGroup { get; set; } = "Student";
}
