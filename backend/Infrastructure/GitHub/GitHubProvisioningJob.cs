using Backend.Application.Services;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.GitHub;

/// <summary>
/// Skapar pending elevrepon i bakgrunden. Publicering behöver därför inte vänta på GitHub, och
/// misslyckade försök kan köras om utan att läraren publicerar uppgiften igen.
/// </summary>
public class GitHubProvisioningJob(
    IServiceScopeFactory scopeFactory,
    IOptions<GitHubProvisioningOptions> options,
    ILogger<GitHubProvisioningJob> logger) : BackgroundService
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
            var service = scope.ServiceProvider.GetRequiredService<GitHubRepoProvisioningService>();
            var ready = await service.ProcessPendingAsync(ct);
            if (ready > 0)
            {
                logger.LogInformation("Provisioned {Count} GitHub repositories", ready);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Servern stängs.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The GitHub provisioning job failed");
        }
    }
}
