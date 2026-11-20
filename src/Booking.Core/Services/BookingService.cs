using Booking.Core.Calendar;
using Booking.Core.Data;
using Booking.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BookingEntity = Booking.Core.Entities.Booking;

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
                ? new ResourceSlot(r.Id, r.Code, r.Name, r.Type, b.Id, b.PersonId, b.PersonName, r.Location, r.HasCharger)
                : new ResourceSlot(r.Id, r.Code, r.Name, r.Type, null, null, null, r.Location, r.HasCharger))
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

    public async Task<BookingResult> BookAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Check order: NotFound → OutOfRange → Weekend → PersonInactive → ResourceInactive.
        var person = await db.People.AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, ct);
        var resource = await db.Resources.AsNoTracking().SingleOrDefaultAsync(r => r.Id == resourceId, ct);
        if (person is null || resource is null)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        if (date < calendar.Today || date > calendar.LastBookableDay)
        {
            return BookingResult.Fail(BookingError.OutOfRange);
        }
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return BookingResult.Fail(BookingError.Weekend);
        }
        if (!person.IsActive)
        {
            return BookingResult.Fail(BookingError.PersonInactive);
        }
        if (!resource.IsActive)
        {
            return BookingResult.Fail(BookingError.ResourceInactive);
        }

        // Pre-checks give the normal case a clean answer...
        if (await db.Bookings.AnyAsync(b => b.ResourceId == resource.Id && b.Date == date, ct))
        {
            return BookingResult.Fail(BookingError.ResourceTaken);
        }
        if (await db.Bookings.AnyAsync(b => b.PersonId == person.Id && b.ResourceType == resource.Type && b.Date == date, ct))
        {
            return BookingResult.Fail(BookingError.AlreadyBookedThatType);
        }

        var booking = new BookingEntity
        {
            PersonId = person.Id,
            ResourceId = resource.Id,
            ResourceType = resource.Type,
            Date = date,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ConflictFrom(ex) is { } conflict)
        {
            // ...and the unique indexes decide simultaneous clicks that both passed the pre-checks.
            return BookingResult.Fail(conflict);
        }

        return BookingResult.Ok(booking.Id);
    }

    private static BookingError? ConflictFrom(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            ? pg.ConstraintName switch
            {
                BookingDbContext.ResourceDateIndex => BookingError.ResourceTaken,
                BookingDbContext.PersonTypeDateIndex => BookingError.AlreadyBookedThatType,
                _ => null
            }
            : null;

    public async Task<BookingResult> CancelAsync(int personId, int bookingId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Check order: NotFound → NotYourBooking → OutOfRange (only today or later can be cancelled).
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == bookingId, ct);
        if (booking is null)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        if (booking.PersonId != personId)
        {
            return BookingResult.Fail(BookingError.NotYourBooking);
        }
        if (booking.Date < calendar.Today)
        {
            return BookingResult.Fail(BookingError.OutOfRange);
        }

        // A single DELETE: if a simultaneous cancel got there first, nothing is deleted and we report NotFound.
        var deleted = await db.Bookings
            .Where(b => b.Id == bookingId && b.PersonId == personId)
            .ExecuteDeleteAsync(ct);

        return deleted == 1
            ? new BookingResult(true, null, null)
            : BookingResult.Fail(BookingError.NotFound);
    }
}
