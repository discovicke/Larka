namespace Backend.Application.Services;

public sealed class GitHubProvisioningOptions
{
    public const string SectionName = "GitHub:ProvisioningJob";

    /// <summary>Av i testerna, som anropar tjänsten direkt i stället för att vänta på jobbet.</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 20;

    public int MaxAttempts { get; set; } = 5;
}
