namespace Booking.Domain.Bookings;

/// <summary>
/// One person on one desk or parking spot for one day (aggregate root).
/// Rules about a single booking live here; rules across bookings (one per spot, one of each type per person per day)
/// are checked by the application and guaranteed by unique indexes when two people click at once.
/// </summary>
public class Booking
{
    private Booking()
    {
    }

    public int Id { get; private set; }
    public int PersonId { get; private set; }
    public Person Person { get; private set; } = null!;
    public int ResourceId { get; private set; }
    public Resource Resource { get; private set; } = null!;

    /// <summary>Copied from the resource so the database can enforce one desk and one parking spot per person per day.</summary>
    public ResourceType ResourceType { get; private set; }

    public DateOnly Date { get; private set; }

    /// <summary>Always UTC.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Check order: OutOfRange → Weekend → PersonInactive → ResourceInactive.</summary>
    public static Result<Booking, BookingError> Book(Person person, Resource resource, DateOnly date, IOfficeCalendar calendar, DateTime createdAtUtc)
    {
        BookingError? error =
            date < calendar.Today || date > calendar.LastBookableDay ? BookingError.OutOfRange
            : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? BookingError.Weekend
            : !person.IsActive ? BookingError.PersonInactive
            : !resource.IsActive ? BookingError.ResourceInactive
            : null;
        return error is { } refused
            ? Result<Booking, BookingError>.Fail(refused)
            : Result<Booking, BookingError>.Ok(Reconstitute(person.Id, resource.Id, resource.Type, date, createdAtUtc));
    }

    /// <summary>Only your own booking, and only for today or later. Check order: NotYourBooking → OutOfRange.</summary>
    public BookingError? CanBeCancelledBy(int personId, DateOnly today) =>
        PersonId != personId ? BookingError.NotYourBooking
        : Date < today ? BookingError.OutOfRange
        : null;

    /// <summary>A booking as stored, without checking the rules: for tests that set up past or otherwise unbookable days.</summary>
    internal static Booking Reconstitute(int personId, int resourceId, ResourceType type, DateOnly date, DateTime createdAtUtc) =>
        new() { PersonId = personId, ResourceId = resourceId, ResourceType = type, Date = date, CreatedAt = createdAtUtc };
}
