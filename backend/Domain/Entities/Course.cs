namespace Backend.Domain.Entities;

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = "";
    public string Code { get; set; } = "";
}
