namespace Backend.Infrastructure.Persistence;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"Postgres" (dev och prod) eller "Sqlite" (testerna).</summary>
    public string Provider { get; set; } = "Postgres";

    /// <summary>Lägger in exempeldata om databasen är tom. Bara i Development.</summary>
    public bool SeedDevData { get; set; }
}
