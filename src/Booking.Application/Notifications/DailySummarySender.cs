using Booking.Application.Admin;
using Booking.Application.Bookings;
using Microsoft.Extensions.Logging;

namespace Booking.Application.Notifications;

/// <summary>
/// Sends today's summary as a separate email to every active person with an email address.
/// One failing address is logged and counted; it never stops the others.
/// </summary>
public sealed class DailySummarySender(
    IBookingService bookings,
    IAdminService admin,
    IOfficeCalendar calendar,
    IEmailSender emails,
    ILogger<DailySummarySender> logger)
{
    public async Task<SummaryResult> SendTodayAsync(CancellationToken ct)
    {
        var day = await bookings.GetDayAsync(calendar.Today, ct);
        var recipients = (await admin.GetPeopleAsync(ct))
            .Where(p => p.IsActive && !string.IsNullOrWhiteSpace(p.Email));

        int sent = 0, failed = 0;
        foreach (var person in recipients)
        {
            try
            {
                await emails.SendAsync(DailySummaryComposer.Compose(day, person), ct);
                sent++;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                failed++;
                logger.LogError(ex, "Daily summary to {Email} failed", person.Email);
            }
        }

        logger.LogInformation("Daily summary for {Date}: {Sent} sent, {Failed} failed", day.Date, sent, failed);
        return new SummaryResult(sent, failed);
    }
}
