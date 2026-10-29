namespace Backend.Domain.Entities;

// Eleven har bockat av en punkt i uppgiftens checklista ("- [ ] ..." i texten). Punkten identifieras av sin
// plats bland checklistepunkterna (ItemIndex, från 0). En rad finns bara för avbockade punkter.
public class ChecklistTick
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentLinkId { get; set; }
    public Guid StudentId { get; set; }
    public int ItemIndex { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}
