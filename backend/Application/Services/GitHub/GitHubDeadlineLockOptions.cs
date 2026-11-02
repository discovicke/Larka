namespace Backend.Application.Services;

public sealed class GitHubDeadlineLockOptions
{
    public const string SectionName = "GitHub:DeadlineLockJob";

    /// <summary>Av i testerna, som anropar tjänsten direkt.</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 50;
}
