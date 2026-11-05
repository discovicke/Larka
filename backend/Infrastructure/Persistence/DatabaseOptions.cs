namespace Backend.Infrastructure.Persistence;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Lägger in demokontona och realistisk demodata (DemoSeed) vid start. Av som standard. Edugrades demo sätter den
    /// i Infisical (prod).
    /// </summary>
    public bool SeedDemoData { get; set; }

    /// <summary>
    /// Mallrepot (ägare/namn) för demodatans GitHub-uppgift. Är det satt görs uppgiften om till en repo-uppgift vid
    /// start, och varje elev får ett repo som bakgrundsjobbet skapar. Tomt betyder att uppgiften lämnas in som text.
    /// </summary>
    public string? DemoTemplateRepo { get; set; }
}
