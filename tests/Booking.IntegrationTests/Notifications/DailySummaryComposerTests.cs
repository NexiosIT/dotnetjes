using Booking.Application.Admin;
using Booking.Application.Notifications;

namespace Booking.IntegrationTests.Notifications;

public sealed class DailySummaryComposerTests
{
    private static readonly DateOnly Friday = new(2026, 11, 20);
    private static readonly PersonView Anna = new PersonView(1, "Anna", "anna@example.com", true);

    [Fact]
    public void WhenComposingTheSummary_ThenTheSubjectNamesTheDayAndItIsSentToTheRecipient()
    {
        var email = DailySummaryComposer.Compose(Days.With(Friday), Anna);

        Assert.Equal("Who's in the office – Fri 20 Nov", email.Subject);
        Assert.Equal("anna@example.com", email.To);
    }

    [Fact]
    public void GivenBookingsByAnnaAndBen_WhenComposingTheSummary_ThenItListsTheirNamesAndSpots()
    {
        var day = Days.With(Friday, ("D2", 1, "Anna"), ("P2", 1, "Anna"), ("D3", 2, "Ben"));

        var body = DailySummaryComposer.Compose(day, Anna).HtmlBody;

        Assert.Contains("Anna", body);
        Assert.Contains("D2", body);
        Assert.Contains("P2", body);
        Assert.Contains("Ben", body);
        Assert.Contains("D3", body);
    }

    [Fact]
    public void GivenThreeBookedSpots_WhenComposingTheSummary_ThenItCountsTheFreeDesksAndParkingSpots()
    {
        var day = Days.With(Friday, ("D2", 1, "Anna"), ("P2", 1, "Anna"), ("D3", 2, "Ben"));

        var body = DailySummaryComposer.Compose(day, Anna).HtmlBody;

        Assert.Contains("Free: 6 desks, 3 parking spots", body);
    }

    [Fact]
    public void GivenNoBookings_WhenComposingTheSummary_ThenItSaysNobodyHasBooked()
    {
        var body = DailySummaryComposer.Compose(Days.With(Friday), Anna).HtmlBody;

        Assert.Contains("Nobody has booked for today.", body);
    }

    [Fact]
    public void GivenANameWithAnAmpersand_WhenComposingTheSummary_ThenTheNameIsHtmlEncoded()
    {
        var day = Days.With(Friday, ("D1", 7, "Tom & Jerry"));

        var body = DailySummaryComposer.Compose(day, Anna).HtmlBody;

        Assert.Contains("Tom &amp; Jerry", body);
        Assert.DoesNotContain("Tom & Jerry", body);
    }
}
