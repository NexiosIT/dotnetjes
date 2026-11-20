using Booking.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Booking.Application.Waitlist;

/// <summary>
/// Emails everyone waiting for a charger spot on the day one freed up: active people with an email address,
/// not the person who freed it, and not anyone who has a charger spot that day by now.
/// One failing address is logged and counted; it never stops the others.
/// </summary>
public sealed class WaitlistEmailSender(
    IUnitOfWorkFactory unitsOfWork,
    IEmailSender emails,
    WaitlistEmailSettings settings,
    ILogger<WaitlistEmailSender> logger)
{
    public async Task<SummaryResult> SendAsync(ChargerSpotFreed freed, CancellationToken ct)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);

        var spot = await uow.Resources.GetAsync(freed.ResourceId, ct);
        if (spot is not { IsChargerSpot: true })
        {
            return new SummaryResult(0, 0);
        }

        var recipients = await uow.Waitlist.WaitersToNotifyAsync(freed.Date, freed.FreedByPersonId, ct);

        int sent = 0, failed = 0;
        foreach (var person in recipients)
        {
            try
            {
                await emails.SendAsync(WaitlistEmailComposer.Compose(person, freed.Date, spot, settings.PublicUrl), ct);
                sent++;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                failed++;
                logger.LogError(ex, "Waitlist email to {Email} failed", person.Email);
            }
        }

        logger.LogInformation("Waitlist email for {Date}: {Sent} sent, {Failed} failed", freed.Date, sent, failed);
        return new SummaryResult(sent, failed);
    }
}
