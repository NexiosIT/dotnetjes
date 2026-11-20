using Booking.Application.Waitlist;

namespace Booking.Infrastructure.BackgroundJobs;

/// <summary>
/// Sends the waitlist emails for each queued "charger spot freed" message, one at a time.
/// Any failure is logged and the loop carries on; it never takes the app down.
/// </summary>
public sealed class WaitlistNotificationService(
    ChannelWaitlistNotifier notifier,
    IServiceScopeFactory scopes,
    ILogger<WaitlistNotificationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var freed in notifier.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<WaitlistEmailSender>().SendAsync(freed, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Waitlist emails for charger spot {ResourceId} on {Date} failed", freed.ResourceId, freed.Date);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
