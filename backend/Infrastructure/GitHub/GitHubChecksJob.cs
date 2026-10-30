using Backend.Application.Services;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.GitHub;

/// <summary>
/// Fallback när webhooks missas: uppdaterar alla elevrepon med jämna mellanrum.
/// </summary>
public class GitHubChecksJob(
    IServiceScopeFactory scopeFactory,
    IOptions<GitHubChecksOptions> options,
    ILogger<GitHubChecksJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || settings.IntervalMinutes <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.IntervalMinutes));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<SubmissionService>()
                    .RefreshChecksAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Servern stängs.
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The GitHub checks job failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
