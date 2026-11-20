using Microsoft.Extensions.Time.Testing;

namespace Booking.Domain.Tests;

public sealed class OfficeCalendarTests
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    private static OfficeCalendar At(string utc) =>
        new(new FakeTimeProvider(DateTimeOffset.Parse(utc)), Brussels, maxDaysAhead: 14);

    [Fact]
    public void GivenALateUTCTime_WhenAskingForToday_ThenTheDateIsComputedInBrussels() =>
        Assert.Equal(new DateOnly(2026, 11, 21), At("2026-11-20T23:30:00Z").Today);

    [Fact]
    public void GivenAWeekday_WhenAskingForTheFirstBookableDay_ThenItIsToday() =>
        Assert.Equal(new DateOnly(2026, 11, 18), At("2026-11-18T08:00:00Z").FirstBookableDay);

    [Fact]
    public void GivenAWeekendDay_WhenAskingForTheFirstBookableDay_ThenItIsTheNextMonday() =>
        Assert.Equal(new DateOnly(2026, 11, 23), At("2026-11-21T10:00:00Z").FirstBookableDay);

    [Fact]
    public void WhenAskingForTheLastBookableDay_ThenItIsTodayPlusTheMaximumDaysAhead() =>
        Assert.Equal(new DateOnly(2026, 12, 2), At("2026-11-18T08:00:00Z").LastBookableDay);

    [Theory]
    [InlineData("2026-11-17", false)] // yesterday
    [InlineData("2026-11-18", true)]  // today
    [InlineData("2026-11-21", false)] // Saturday
    [InlineData("2026-12-02", true)]  // today + 14
    [InlineData("2026-12-03", false)] // today + 15
    public void GivenADate_WhenCheckingIfItIsBookable_ThenOnlyWeekdaysWithinTheWindowAre(string date, bool expected) =>
        Assert.Equal(expected, At("2026-11-18T08:00:00Z").IsBookable(DateOnly.Parse(date)));

    [Fact]
    public void GivenAWeekend_WhenNavigatingToNextOrPreviousWeekday_ThenItIsSkipped()
    {
        var calendar = At("2026-11-18T08:00:00Z");
        Assert.Equal(new DateOnly(2026, 11, 23), calendar.NextWeekday(new DateOnly(2026, 11, 20)));
        Assert.Equal(new DateOnly(2026, 11, 20), calendar.PreviousWeekday(new DateOnly(2026, 11, 23)));
    }
}
