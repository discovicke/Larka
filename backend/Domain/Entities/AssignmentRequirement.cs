namespace Backend.Domain.Entities;

// Ett krav på en GitHub-uppgift. Antingen kopplat till ett testmönster eller manuellt bedömt.
public class AssignmentRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public string Title { get; set; } = "";
    public string? TestPattern { get; set; }
    public bool IsManual { get; set; }
    public int Position { get; set; }
}
