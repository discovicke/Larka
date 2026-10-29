using Backend.Application.Services;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.GitHub;

/// <summary>Låser elevrepon när uppgiftens deadline har passerat.</summary>
public class GitHubDeadlineLockJob(
    IServiceScopeFactory scopeFactory,
    IOptions<GitHubDeadlineLockOptions> options,
    ILogger<GitHubDeadlineLockJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || settings.IntervalSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.IntervalSeconds));
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<GitHubDeadlineLockService>();
            var locked = await service.ProcessDueAsync(ct);
            if (locked > 0)
            {
                logger.LogInformation("Locked {Count} GitHub repositories at deadline", locked);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Servern stängs.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The GitHub deadline lock job failed");
        }
    }
}
