using Booking.Core.Data;
using Booking.Core.Waitlist;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BookingEntity = Booking.Domain.Bookings.Booking;

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

        var resources = await db.Resources
            .AsNoTracking()
            .Where(r => r.IsActive || db.Bookings.Any(b => b.ResourceId == r.Id && b.Date == date))
            .OrderBy(r => r.Code)
            .ToListAsync(ct);

        var slots = resources
            .Select(r => bookings.TryGetValue(r.Id, out var b)
                ? new ResourceSlot(r.Id, r.Code, r.Name, r.Type, b.Id, b.PersonId, b.PersonName, r.Location, r.HasCharger, r.IsActive)
                : new ResourceSlot(r.Id, r.Code, r.Name, r.Type, null, null, null, r.Location, r.HasCharger, r.IsActive))
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

        var (person, resource) = await LoadAsync(db, personId, resourceId, ct);
        if (person is null || resource is null)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        var made = BookingEntity.Book(person, resource, date, calendar, time.GetUtcNow().UtcDateTime);
        if (made.Error is { } refused)
        {
            return BookingResult.Fail(refused);
        }

        if (await db.Bookings.AnyAsync(b => b.ResourceId == resource.Id && b.Date == date, ct))
        {
            return BookingResult.Fail(BookingError.ResourceTaken);
        }
        if (await db.Bookings.AnyAsync(b => b.PersonId == person.Id && b.ResourceType == resource.Type && b.Date == date, ct))
        {
            return BookingResult.Fail(BookingError.AlreadyBookedThatType);
        }

        var booking = made.Value!;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (resource.IsChargerSpot)
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

        var booking = await db.Bookings.AsNoTracking().Include(b => b.Resource).SingleOrDefaultAsync(b => b.Id == bookingId, ct);
        if (booking is null)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        if (booking.CanBeCancelledBy(personId, calendar.Today) is { } refused)
        {
            return BookingResult.Fail(refused);
        }

        var deleted = await db.Bookings
            .Where(b => b.Id == bookingId && b.PersonId == personId)
            .ExecuteDeleteAsync(ct);

        if (deleted != 1)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }

        if (booking.Resource.IsChargerSpot)
        {
            notifier.ChargerSpotFreed(new ChargerSpotFreed(booking.Date, booking.ResourceId, personId));
        }
        return new BookingResult(true, null, null);
    }

    public async Task<BookingResult> SwitchAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var (person, resource) = await LoadAsync(db, personId, resourceId, ct);
        if (person is null || resource is null)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        var made = BookingEntity.Book(person, resource, date, calendar, time.GetUtcNow().UtcDateTime);
        if (made.Error is { } refused)
        {
            return BookingResult.Fail(refused);
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

        var booking = made.Value!;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Bookings.Remove(old);
        await db.SaveChangesAsync(ct);
        if (resource.IsChargerSpot)
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

        if (old.Resource.IsChargerSpot)
        {
            notifier.ChargerSpotFreed(new ChargerSpotFreed(date, old.ResourceId, person.Id));
        }
        return BookingResult.Ok(booking.Id);
    }

    private static async Task<(Person? Person, Resource? Resource)> LoadAsync(
        BookingDbContext db, int personId, int resourceId, CancellationToken ct) =>
        (await db.People.AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, ct),
         await db.Resources.AsNoTracking().SingleOrDefaultAsync(r => r.Id == resourceId, ct));
}
