using Booking.Application.Waitlist;
using BookingEntity = Booking.Domain.Bookings.Booking;

namespace Booking.Application.Bookings;

/// <summary>
/// Book, switch and cancel. Single-booking rules come from <see cref="BookingEntity"/>; rules across bookings are
/// pre-checked here for a clean answer, and the database's unique indexes decide simultaneous clicks.
/// </summary>
public sealed class BookingService(
    IUnitOfWorkFactory unitsOfWork,
    IDayOverviewQuery days,
    IOfficeCalendar calendar,
    TimeProvider time,
    IWaitlistNotifier notifier) : IBookingService
{
    public Task<DayOverview> GetDayAsync(DateOnly date, CancellationToken ct = default) => days.GetDayAsync(date, ct);

    /// <summary>Getting a charger spot takes you off that day's waitlist, in the same transaction as the booking.</summary>
    public async Task<BookingResult> BookAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);

        var person = await uow.People.GetAsync(personId, ct);
        var resource = await uow.Resources.GetAsync(resourceId, ct);
        if (person is null || resource is null)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        var made = BookingEntity.Book(person, resource, date, calendar, time.GetUtcNow().UtcDateTime);
        if (made.Error is { } refused)
        {
            return BookingResult.Fail(refused);
        }
        if (await uow.Bookings.IsResourceTakenAsync(resource.Id, date, ct))
        {
            return BookingResult.Fail(BookingError.ResourceTaken);
        }
        if (await uow.Bookings.HasBookingOfTypeAsync(person.Id, resource.Type, date, ct))
        {
            return BookingResult.Fail(BookingError.AlreadyBookedThatType);
        }

        var booking = made.Value!;
        await uow.BeginTransactionAsync(ct);
        if (resource.IsChargerSpot)
        {
            await uow.Waitlist.RemoveAsync(person.Id, date, ct);
        }
        uow.Bookings.Add(booking);
        if (await uow.SaveChangesAsync(ct) is { } conflict)
        {
            return BookingResult.Fail(ToBookingError(conflict));
        }
        await uow.CommitAsync(ct);
        return BookingResult.Ok(booking.Id);
    }

    public async Task<BookingResult> CancelAsync(int personId, int bookingId, CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);

        var booking = await uow.Bookings.GetWithResourceAsync(bookingId, ct);
        if (booking is null)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        if (booking.CanBeCancelledBy(personId, calendar.Today) is { } refused)
        {
            return BookingResult.Fail(refused);
        }
        if (!await uow.Bookings.DeleteAsync(bookingId, personId, ct))
        {
            return BookingResult.Fail(BookingError.NotFound);
        }

        if (booking.Resource.IsChargerSpot)
        {
            notifier.ChargerSpotFreed(new ChargerSpotFreed(booking.Date, booking.ResourceId, personId));
        }
        return new BookingResult(true, null, null);
    }

    /// <summary>
    /// Releases the old spot and takes the new one together: if the new one is lost to a simultaneous click,
    /// the rollback gives the old spot back.
    /// </summary>
    public async Task<BookingResult> SwitchAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);

        var person = await uow.People.GetAsync(personId, ct);
        var resource = await uow.Resources.GetAsync(resourceId, ct);
        if (person is null || resource is null)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        var made = BookingEntity.Book(person, resource, date, calendar, time.GetUtcNow().UtcDateTime);
        if (made.Error is { } refused)
        {
            return BookingResult.Fail(refused);
        }
        var old = await uow.Bookings.FindAsync(person.Id, resource.Type, date, ct);
        if (old is null)
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        if (old.ResourceId == resource.Id)
        {
            return BookingResult.Ok(old.Id);
        }
        if (await uow.Bookings.IsResourceTakenAsync(resource.Id, date, ct))
        {
            return BookingResult.Fail(BookingError.ResourceTaken);
        }

        var booking = made.Value!;
        await uow.BeginTransactionAsync(ct);
        if (!await uow.Bookings.DeleteAsync(old.Id, person.Id, ct))
        {
            return BookingResult.Fail(BookingError.NotFound);
        }
        if (resource.IsChargerSpot)
        {
            await uow.Waitlist.RemoveAsync(person.Id, date, ct);
        }
        uow.Bookings.Add(booking);
        if (await uow.SaveChangesAsync(ct) is { } conflict)
        {
            return BookingResult.Fail(ToBookingError(conflict));
        }
        await uow.CommitAsync(ct);

        if (old.Resource.IsChargerSpot)
        {
            notifier.ChargerSpotFreed(new ChargerSpotFreed(date, old.ResourceId, person.Id));
        }
        return BookingResult.Ok(booking.Id);
    }

    private static BookingError ToBookingError(SaveConflict conflict) => conflict switch
    {
        SaveConflict.ResourceTaken => BookingError.ResourceTaken,
        SaveConflict.AlreadyBookedThatType => BookingError.AlreadyBookedThatType,
        _ => throw new InvalidOperationException($"Unexpected conflict while booking: {conflict}")
    };
}
