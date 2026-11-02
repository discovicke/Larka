namespace Backend.Application.Services;

public sealed class GitHubSupportOptions
{
    public const string SectionName = "GitHub:Support";

    /// <summary>Hur länge ett repo får vara utan elevens första commit innan det räknas som "inte startat".</summary>
    public int NotStartedAfterDays { get; set; } = 3;

    /// <summary>Hur länge en påbörjad uppgift får vara utan push innan den flaggas.</summary>
    public int NoPushAfterDays { get; set; } = 4;

    /// <summary>Hur länge checks får vara röda innan de flaggas.</summary>
    public int RedChecksAfterHours { get; set; } = 36;
}
