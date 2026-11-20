using Booking.Application.Notifications;

namespace Booking.IntegrationTests.Notifications;

public sealed class SummaryScheduleTests
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");
    private static readonly TimeOnly SevenAm = new(7, 0);

    private static DateTimeOffset Utc(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenItIsBeforeSeven_WhenCalculatingTheNextRun_ThenItIsSevenToday() =>
        Assert.Equal(Utc(11, 18, 6), SummarySchedule.NextRunUtc(Utc(11, 18, 5), Brussels, SevenAm));

    [Fact]
    public void GivenItIsExactlySeven_WhenCalculatingTheNextRun_ThenItIsSevenTheNextDay() =>
        Assert.Equal(Utc(11, 19, 6), SummarySchedule.NextRunUtc(Utc(11, 18, 6), Brussels, SevenAm));

    [Fact]
    public void GivenItIsFridayAfterSeven_WhenCalculatingTheNextRun_ThenItIsSevenOnMonday() =>
        Assert.Equal(Utc(11, 23, 6), SummarySchedule.NextRunUtc(Utc(11, 20, 8), Brussels, SevenAm));

    [Fact]
    public void GivenItIsSaturday_WhenCalculatingTheNextRun_ThenItIsSevenOnMonday() =>
        Assert.Equal(Utc(11, 23, 6), SummarySchedule.NextRunUtc(Utc(11, 21, 10), Brussels, SevenAm));

    [Fact]
    public void GivenItIsSummerTime_WhenCalculatingTheNextRun_ThenSevenLocalTimeIsUsed() =>
        Assert.Equal(Utc(6, 15, 5), SummarySchedule.NextRunUtc(Utc(6, 15, 4), Brussels, SevenAm));
}
