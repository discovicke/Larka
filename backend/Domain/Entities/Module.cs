namespace Backend.Domain.Entities;

// En del av en kurs, t.ex. en vecka ("Vecka 2 – E2E och releasehantering"). Innehåll hamnar i en modul
// via ContentLink.ModuleId. Position är ordningen i kursen: 1, 2, 3 … utan luckor.
// PublishAt är veckans egen grind: eleven ser inget i veckan förrän datumet passerat. Null = direkt.
public class Module
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Position { get; set; }
    public DateTimeOffset? PublishAt { get; set; }
}
