using Booking.Core.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Core.Waitlist;

/// <summary>Waitlist rules (charger waitlist design). One DbContext per method.</summary>
public sealed class WaitlistService(
    IDbContextFactory<BookingDbContext> dbFactory,
    IOfficeCalendar calendar,
    TimeProvider time) : IWaitlistService
{
    public async Task<WaitlistResult> JoinAsync(int personId, DateOnly date, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var person = await db.People.AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, ct);
        if (person is null)
        {
            return WaitlistResult.Fail(WaitlistError.NotFound);
        }
        var joined = WaitlistEntry.Join(person, date, calendar, time.GetUtcNow().UtcDateTime);
        if (joined.Error is { } refused)
        {
            return WaitlistResult.Fail(refused);
        }
        if (await db.Bookings.AnyAsync(b => b.PersonId == personId && b.Date == date && b.Resource.HasCharger && b.Resource.Type == ResourceType.Parking, ct))
        {
            return WaitlistResult.Fail(WaitlistError.AlreadyHasCharger);
        }
        if (await db.Resources.AnyAsync(r => r.Type == ResourceType.Parking && r.HasCharger && r.IsActive
                && !db.Bookings.Any(b => b.ResourceId == r.Id && b.Date == date), ct))
        {
            return WaitlistResult.Fail(WaitlistError.ChargerSpotFree);
        }
        if (await db.Waitlist.AnyAsync(w => w.PersonId == personId && w.Date == date, ct))
        {
            return WaitlistResult.Ok();
        }

        db.Waitlist.Add(joined.Value!);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: BookingDbContext.WaitlistIndex })
        {
        }
        return WaitlistResult.Ok();
    }

    public async Task LeaveAsync(int personId, DateOnly date, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Waitlist.Where(w => w.PersonId == personId && w.Date == date).ExecuteDeleteAsync(ct);
    }

    public async Task<bool> IsOnWaitlistAsync(int personId, DateOnly date, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Waitlist.AnyAsync(w => w.PersonId == personId && w.Date == date, ct);
    }

    public async Task<bool> HasEmailAsync(int personId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var email = await db.People.Where(p => p.Id == personId).Select(p => p.Email).SingleOrDefaultAsync(ct);
        return !string.IsNullOrWhiteSpace(email);
    }
}
