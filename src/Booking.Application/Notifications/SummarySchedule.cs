namespace Booking.Application.Notifications;

public static class SummarySchedule
{
    /// <summary>The next weekday at <paramref name="sendAt"/> office time, strictly after <paramref name="nowUtc"/>.</summary>
    public static DateTimeOffset NextRunUtc(DateTimeOffset nowUtc, TimeZoneInfo zone, TimeOnly sendAt)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);

        for (var date = localDate; ; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var local = date.ToDateTime(sendAt, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local))
            {
                local = local.AddHours(1);
            }

            var run = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
            if (run > nowUtc)
            {
                return run;
            }
        }
    }
}
