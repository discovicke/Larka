namespace Backend.Domain.Entities;

// Publicerar content till en grupp, en kurs eller båda (minst en måste vara satt):
// - bara grupp: t.ex. en nyhet till NET25
// - bara kurs: alla grupper som läser kursen
// - grupp + kurs: kursen så som den går för just den gruppen
// Deadline och pin ligger här, så samma uppgift kan ha olika deadline för olika klasser.
// ModuleId lägger innehållet i en av kursens moduler (veckor). Kräver CourseId, och modulen ska höra till kursen.
// Position är ordningen i modulen: 1, 2, 3 … utan luckor. Länkar utan modul har 0.
public class ContentLink : IHasPosition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? ModuleId { get; set; }
    public int Position { get; set; }
    public DateTimeOffset PublishAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DueAt { get; set; }
    public bool IsPinned { get; set; }

    // Projektgrupper för grupprepon. Tom för individuella uppgifter.
    public List<AssignmentGroup> Groups { get; set; } = [];
}
