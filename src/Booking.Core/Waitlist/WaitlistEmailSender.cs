using Booking.Core.Data;
using Booking.Core.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Booking.Core.Waitlist;

/// <summary>
/// Emails everyone waiting for a charger spot on the day one freed up: active people with an email address,
/// not the person who freed it, and not anyone who has a charger spot that day by now.
/// One failing address is logged and counted; it never stops the others.
/// </summary>
public sealed class WaitlistEmailSender(
    IDbContextFactory<BookingDbContext> dbFactory,
    IEmailSender emails,
    WaitlistEmailSettings settings,
    ILogger<WaitlistEmailSender> logger)
{
    public async Task<SummaryResult> SendAsync(ChargerSpotFreed freed, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var spot = await db.Resources.AsNoTracking().SingleOrDefaultAsync(r => r.Id == freed.ResourceId, ct);
        if (spot is not { Type: ResourceType.Parking, HasCharger: true, IsActive: true })
        {
            return new SummaryResult(0, 0);
        }

        var recipients = await db.Waitlist.AsNoTracking()
            .Where(w => w.Date == freed.Date && w.PersonId != freed.FreedByPersonId)
            .Select(w => w.Person)
            .Where(p => p.IsActive && p.Email != null && p.Email.Trim() != "")
            .Where(p => !db.Bookings.Any(b => b.PersonId == p.Id && b.Date == freed.Date
                && b.Resource.Type == ResourceType.Parking && b.Resource.HasCharger))
            .ToListAsync(ct);

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
