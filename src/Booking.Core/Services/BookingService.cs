using Booking.Core.Calendar;
using Booking.Core.Data;
using Booking.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Booking.Core.Services;

/// <summary>The real booking rules (spec §5). One DbContext per method, disposed at the end.</summary>
public sealed class BookingService(
    IDbContextFactory<BookingDbContext> dbFactory,
    IOfficeCalendar calendar,
    TimeProvider time) : IBookingService
{
    public async Task<DayOverview> GetDayAsync(DateOnly date, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var bookings = await db.Bookings
            .AsNoTracking()
            .Where(b => b.Date == date)
            .Select(b => new { b.Id, b.ResourceId, b.PersonId, PersonName = b.Person.Name })
            .ToDictionaryAsync(b => b.ResourceId, ct);

        // Active resources, plus inactive ones that still have a booking that day (rule 7).
        var resources = await db.Resources
            .AsNoTracking()
            .Where(r => r.IsActive || db.Bookings.Any(b => b.ResourceId == r.Id && b.Date == date))
            .OrderBy(r => r.Code)
            .ToListAsync(ct);

        var slots = resources
            .Select(r => bookings.TryGetValue(r.Id, out var b)
                ? new ResourceSlot(r.Id, r.Code, r.Name, r.Type, b.Id, b.PersonId, b.PersonName)
                : new ResourceSlot(r.Id, r.Code, r.Name, r.Type, null, null, null))
            .ToList();

        var peopleInOffice = bookings.Values
            .Select(b => b.PersonName)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        return new DayOverview(
            date,
            slots.Where(s => s.Type == ResourceType.Desk).ToList(),
            slots.Where(s => s.Type == ResourceType.Parking).ToList(),
            peopleInOffice);
    }

    public Task<BookingResult> BookAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default) =>
        throw new NotImplementedException("Task A2/A3");

    public Task<BookingResult> CancelAsync(int personId, int bookingId, CancellationToken ct = default) =>
        throw new NotImplementedException("Task A4");
}
