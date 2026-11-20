namespace Booking.Domain.Calendar;

/// <summary>
/// The single source of truth for "today" and which days can be booked.
/// The UI arrows, the booking rules and the email schedule all use it so they always agree.
/// </summary>
public interface IOfficeCalendar
{
    /// <summary>Today in the office time zone (App:TimeZone).</summary>
    DateOnly Today { get; }

    /// <summary>Today, or the next weekday if today is a weekend.</summary>
    DateOnly FirstBookableDay { get; }

    /// <summary>Today + Booking:MaxDaysAhead.</summary>
    DateOnly LastBookableDay { get; }

    /// <summary>A weekday between today and <see cref="LastBookableDay"/>, inclusive.</summary>
    bool IsBookable(DateOnly date);

    DateOnly NextWeekday(DateOnly date);

    DateOnly PreviousWeekday(DateOnly date);
}
