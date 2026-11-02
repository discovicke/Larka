namespace Backend.Domain.Entities;

// En teoridel som hör ihop med en uppgift. Referensen ligger på utdelningen, så samma
// uppgift kan hänvisa till olika delar i olika kurser.
public class AssignmentReference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceLinkId { get; set; }
    public Guid TargetLinkId { get; set; }
    public int Position { get; set; }
}
