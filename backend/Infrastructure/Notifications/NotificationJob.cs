using Backend.Application.Services;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Notifications;

public class NotificationJobOptions
{
    public const string SectionName = "Notifications:Job";

    /// <summary>Av i testerna, som kör NotificationService.CreateScheduledAsync direkt.</summary>
    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 5;
}

/// <summary>
/// Kör NotificationService.CreateScheduledAsync med jämna mellanrum: notiser för publiceringar vars tid passerat
/// och påminnelser 24 timmar före en deadline. Det som redan har en notis hoppas över, så varje varv är säkert.
/// </summary>
public class NotificationJob(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationJobOptions> options,
    ILogger<NotificationJob> logger) : BackgroundService
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
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Ett fel får inte stoppa jobbet. Nästa varv försöker igen.
    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<NotificationService>();
            var created = await service.CreateScheduledAsync(ct);
            if (created > 0)
            {
                logger.LogInformation("Created {Count} notifications", created);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Servern stängs.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The notification job failed");
        }
    }
}
