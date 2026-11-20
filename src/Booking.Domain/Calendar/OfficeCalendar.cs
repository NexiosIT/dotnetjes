namespace Booking.Domain.Calendar;

public sealed class OfficeCalendar(TimeProvider time, TimeZoneInfo zone, int maxDaysAhead) : IOfficeCalendar
{
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);

    public DateOnly FirstBookableDay
    {
        get
        {
            var today = Today;
            return IsWeekend(today) ? NextWeekday(today) : today;
        }
    }

    public DateOnly LastBookableDay => Today.AddDays(maxDaysAhead);

    public bool IsBookable(DateOnly date) =>
        !IsWeekend(date) && date >= Today && date <= LastBookableDay;

    public DateOnly NextWeekday(DateOnly date)
    {
        do { date = date.AddDays(1); } while (IsWeekend(date));
        return date;
    }

    public DateOnly PreviousWeekday(DateOnly date)
    {
        do { date = date.AddDays(-1); } while (IsWeekend(date));
        return date;
    }

    private static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
