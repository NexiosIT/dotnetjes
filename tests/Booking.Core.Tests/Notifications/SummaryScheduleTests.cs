using Booking.Core.Notifications;

namespace Booking.Core.Tests.Notifications;

public sealed class SummaryScheduleTests
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");
    private static readonly TimeOnly SevenAm = new(7, 0);

    private static DateTimeOffset Utc(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NextRun_later_today() =>
        Assert.Equal(Utc(11, 18, 6), SummarySchedule.NextRunUtc(Utc(11, 18, 5), Brussels, SevenAm));

    [Fact]
    public void NextRun_at_exact_time_is_next_day() =>
        Assert.Equal(Utc(11, 19, 6), SummarySchedule.NextRunUtc(Utc(11, 18, 6), Brussels, SevenAm));

    [Fact]
    public void NextRun_skips_weekend() =>
        Assert.Equal(Utc(11, 23, 6), SummarySchedule.NextRunUtc(Utc(11, 20, 8), Brussels, SevenAm));

    [Fact]
    public void NextRun_from_saturday() =>
        Assert.Equal(Utc(11, 23, 6), SummarySchedule.NextRunUtc(Utc(11, 21, 10), Brussels, SevenAm));

    [Fact]
    public void NextRun_follows_summer_time() =>
        Assert.Equal(Utc(6, 15, 5), SummarySchedule.NextRunUtc(Utc(6, 15, 4), Brussels, SevenAm));
}
