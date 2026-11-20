using Booking.Core.Notifications;

namespace Booking.Core.Tests.Notifications;

public sealed class DailySummaryComposerTests
{
    private static readonly DateOnly Friday = new(2026, 11, 20);
    private static readonly Person Anna = new() { Id = 1, Name = "Anna", Email = "anna@example.com" };

    [Fact]
    public void Compose_subject_names_the_day()
    {
        var email = DailySummaryComposer.Compose(Days.With(Friday), Anna);

        Assert.Equal("Who's in the office – Fri 20 Nov", email.Subject);
        Assert.Equal("anna@example.com", email.To);
    }

    [Fact]
    public void Compose_lists_people_and_spots()
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
    public void Compose_counts_free()
    {
        var day = Days.With(Friday, ("D2", 1, "Anna"), ("P2", 1, "Anna"), ("D3", 2, "Ben"));

        var body = DailySummaryComposer.Compose(day, Anna).HtmlBody;

        Assert.Contains("Free: 6 desks, 3 parking spots", body);
    }

    [Fact]
    public void Compose_empty_day()
    {
        var body = DailySummaryComposer.Compose(Days.With(Friday), Anna).HtmlBody;

        Assert.Contains("Nobody has booked for today.", body);
    }

    [Fact]
    public void Compose_encodes_names()
    {
        var day = Days.With(Friday, ("D1", 7, "Tom & Jerry"));

        var body = DailySummaryComposer.Compose(day, Anna).HtmlBody;

        Assert.Contains("Tom &amp; Jerry", body);
        Assert.DoesNotContain("Tom & Jerry", body);
    }
}
