namespace Backend.Application.Services;

public sealed class GitHubChecksOptions
{
    public const string SectionName = "GitHub:ChecksJob";

    /// <summary>Av i testerna, som uppdaterar repon direkt.</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 360;
}
