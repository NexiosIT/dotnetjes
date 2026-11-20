using System.Globalization;
using Booking.Core.Notifications;

namespace Booking.Web.Notifications;

/// <summary>
/// Sends the daily summary every weekday at Summary:SendAt office time (App:TimeZone).
/// Any failure is logged and the loop carries on to the next day; it never takes the app down.
/// </summary>
public sealed class DailySummaryService(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IConfiguration configuration,
    ILogger<DailySummaryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(configuration["App:TimeZone"] ?? "Europe/Brussels");
        var sendAt = TimeOnly.Parse(configuration["Summary:SendAt"] ?? "07:00", CultureInfo.InvariantCulture);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = SummarySchedule.NextRunUtc(time.GetUtcNow(), zone, sendAt);
            logger.LogInformation("Next daily summary at {NextRun:u}", next);

            try
            {
                await Task.Delay(next - time.GetUtcNow(), time, stoppingToken);

                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<DailySummarySender>();
                await sender.SendTodayAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily summary run failed; trying again at the next scheduled time");
            }
        }
    }
}
