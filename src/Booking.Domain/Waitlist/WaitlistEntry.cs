namespace Booking.Domain.Waitlist;

/// <summary>
/// A person waiting for a charger spot on one day (aggregate root). One entry per person per day.
/// Whether every charger spot is taken, and whether the person already holds one, depends on the whole day:
/// the application checks that.
/// </summary>
public class WaitlistEntry
{
    private WaitlistEntry()
    {
    }

    public int Id { get; private set; }
    public int PersonId { get; private set; }
    public Person Person { get; private set; } = null!;
    public DateOnly Date { get; private set; }

    /// <summary>Always UTC.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Check order: OutOfRange → Weekend → PersonInactive → NoEmail (the waitlist works by email).</summary>
    public static Result<WaitlistEntry, WaitlistError> Join(Person person, DateOnly date, IOfficeCalendar calendar, DateTime createdAtUtc)
    {
        WaitlistError? error =
            date < calendar.Today || date > calendar.LastBookableDay ? WaitlistError.OutOfRange
            : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? WaitlistError.Weekend
            : !person.IsActive ? WaitlistError.PersonInactive
            : !person.HasEmail ? WaitlistError.NoEmail
            : null;
        return error is { } refused
            ? Result<WaitlistEntry, WaitlistError>.Fail(refused)
            : Result<WaitlistEntry, WaitlistError>.Ok(Reconstitute(person.Id, date, createdAtUtc));
    }

    /// <summary>An entry as stored, without checking the rules: for tests.</summary>
    internal static WaitlistEntry Reconstitute(int personId, DateOnly date, DateTime createdAtUtc) =>
        new() { PersonId = personId, Date = date, CreatedAt = createdAtUtc };
}
