namespace Backend.Infrastructure.Persistence;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"Sqlite" (dev) eller "Postgres" (prod).</summary>
    public string Provider { get; set; } = "Sqlite";
}
