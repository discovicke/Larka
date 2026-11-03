namespace Backend.Infrastructure.Persistence;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Lägger in demokontona och realistisk demodata (DemoSeed) vid start. Av som standard. Edugrades demo sätter den
    /// i Infisical (prod).
    /// </summary>
    public bool SeedDemoData { get; set; }
}
