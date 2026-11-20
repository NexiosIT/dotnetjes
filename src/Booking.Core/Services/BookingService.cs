using Booking.Core.Calendar;
using Booking.Core.Data;
using Booking.Core.Entities;
using Booking.Core.Waitlist;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BookingEntity = Booking.Core.Entities.Booking;

namespace Booking.Core.Services;

/// <summary>The real booking rules (spec §5). One DbContext per method, disposed at the end.</summary>
public sealed class BookingService(
    IDbContextFactory<BookingDbContext> dbFactory,
    IOfficeCalendar calendar,
    TimeProvider time,
    IWaitlistNotifier notifier) : IBookingService
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

        var (person, resource, error) = await CheckAsync(db, personId, resourceId, date, ct);
        if (error is not null)
        {
            return BookingResult.Fail(error.Value);
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
        // Getting a charger spot takes you off that day's waitlist, in the same transaction as the booking.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (IsChargerSpot(resource))
        {
            await db.Waitlist.Where(w => w.PersonId == person.Id && w.Date == date).ExecuteDeleteAsync(ct);
        }
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
        await transaction.CommitAsync(ct);

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
        var booking = await db.Bookings.AsNoTracking().Include(b => b.Resource).SingleOrDefaultAsync(b => b.Id == bookingId, ct);
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

        if (deleted != 1)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }

        if (IsChargerSpot(booking.Resource))
        {
            notifier.ChargerSpotFreed(new ChargerSpotFreed(booking.Date, booking.ResourceId, personId));
        }
        return new BookingResult(true, null, null);
    }

    public async Task<BookingResult> SwitchAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Check order: Book's checks → NotFound (no booking of that type to switch from) → ResourceTaken.
        var (person, resource, error) = await CheckAsync(db, personId, resourceId, date, ct);
        if (error is not null)
        {
            return BookingResult.Fail(error.Value);
        }
        var old = await db.Bookings.Include(b => b.Resource)
            .SingleOrDefaultAsync(b => b.PersonId == person.Id && b.ResourceType == resource.Type && b.Date == date, ct);
        if (old is null)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        if (old.ResourceId == resource.Id)
        {
            return BookingResult.Ok(old.Id);
        }
        if (await db.Bookings.AnyAsync(b => b.ResourceId == resource.Id && b.Date == date, ct))
        {
            return BookingResult.Fail(BookingError.ResourceTaken);
        }

        // Release the old spot and take the new one together: if the new one is lost to a simultaneous click,
        // the unique index fails the insert and the rollback gives the old spot back.
        var booking = new BookingEntity
        {
            PersonId = person.Id,
            ResourceId = resource.Id,
            ResourceType = resource.Type,
            Date = date,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Bookings.Remove(old);
        await db.SaveChangesAsync(ct);
        if (IsChargerSpot(resource))
        {
            await db.Waitlist.Where(w => w.PersonId == person.Id && w.Date == date).ExecuteDeleteAsync(ct);
        }
        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ConflictFrom(ex) is { } conflict)
        {
            return BookingResult.Fail(conflict);
        }
        await transaction.CommitAsync(ct);

        if (IsChargerSpot(old.Resource))
        {
            notifier.ChargerSpotFreed(new ChargerSpotFreed(date, old.ResourceId, person.Id));
        }
        return BookingResult.Ok(booking.Id);
    }

    /// <summary>The checks Book and Switch share. Order: NotFound → OutOfRange → Weekend → PersonInactive → ResourceInactive.</summary>
    private async Task<(Person Person, Resource Resource, BookingError? Error)> CheckAsync(
        BookingDbContext db, int personId, int resourceId, DateOnly date, CancellationToken ct)
    {
        var person = await db.People.AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, ct);
        var resource = await db.Resources.AsNoTracking().SingleOrDefaultAsync(r => r.Id == resourceId, ct);
        BookingError? error =
            person is null || resource is null ? BookingError.NotFound
            : date < calendar.Today || date > calendar.LastBookableDay ? BookingError.OutOfRange
            : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? BookingError.Weekend
            : !person.IsActive ? BookingError.PersonInactive
            : !resource.IsActive ? BookingError.ResourceInactive
            : null;
        return (person!, resource!, error);
    }

    /// <summary>The spots the waitlist is about (charger waitlist design).</summary>
    private static bool IsChargerSpot(Resource resource) =>
        resource is { Type: ResourceType.Parking, HasCharger: true, IsActive: true };
}
