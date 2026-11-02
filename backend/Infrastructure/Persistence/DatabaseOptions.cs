namespace Backend.Infrastructure.Persistence;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Lägger in exempeldata om databasen är tom. Bara i Development.</summary>
    public bool SeedDevData { get; set; }

    /// <summary>
    /// Lägger in demokontona och realistisk demodata (DemoSeed) vid start. Av som standard. Edugrades demo sätter den
    /// med DATABASE__SEEDDEMODATA i GitHub-miljön production, som deployen skickar till Render.
    /// </summary>
    public bool SeedDemoData { get; set; }
}
