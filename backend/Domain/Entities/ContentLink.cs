namespace Backend.Domain.Entities;

// Publicerar content till en grupp, en kurs eller båda (minst en måste vara satt):
// - bara grupp: t.ex. en nyhet till NET25
// - bara kurs: alla grupper som läser kursen
// - grupp + kurs: kursen så som den går för just den gruppen
// Deadline och pin ligger här, så samma uppgift kan ha olika deadline för olika klasser.
public class ContentLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? CourseId { get; set; }
    public DateTimeOffset PublishAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DueAt { get; set; }
    public bool IsPinned { get; set; }
}
