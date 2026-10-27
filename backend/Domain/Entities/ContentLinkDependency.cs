namespace Backend.Domain.Entities;

// "BlockedLinkId är låst av BlockingLinkId". Båda är ContentLinks i samma modul (vecka).
// Ingen egen Id: paret är nyckeln, så samma beroende kan bara finnas en gång.
public class ContentLinkDependency
{
    public Guid BlockedLinkId { get; set; }
    public Guid BlockingLinkId { get; set; }
}
