namespace Backend.Infrastructure.Persistence;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Lägger in exempeldata om databasen är tom. Bara i Development.</summary>
    public bool SeedDevData { get; set; }

    /// <summary>
    /// Lägger in realistisk demodata (DemoSeed) vid start, en gång. Bara i prod, via appsettings.Production.json.
    /// </summary>
    public bool SeedDemoData { get; set; }
}
