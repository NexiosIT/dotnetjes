using Booking.Core.Waitlist;

namespace Booking.Core.Tests.Waitlist;

public class WaitlistEmailComposerTests
{
    private static readonly Person Anna = Person.FromLogin("kc-anna", "Anna <A>", "anna@example.com");
    private static readonly Resource P1 = ChargerSpot();

    private static Resource ChargerSpot()
    {
        var spot = Resource.Create("P1", "Parking 1", ResourceType.Parking);
        spot.SetParkingDetails(ParkingLocation.Basement, hasCharger: true);
        return spot;
    }
    private static readonly DateOnly Tuesday = new(2026, 11, 24);

    [Fact]
    public void Subject_names_the_day() =>
        Assert.Equal("A charger spot is free on Tue 24 Nov", WaitlistEmailComposer.Compose(Anna, Tuesday, P1, null).Subject);

    [Fact]
    public void Body_names_spot_and_location()
    {
        var body = WaitlistEmailComposer.Compose(Anna, Tuesday, P1, null).HtmlBody;

        Assert.Contains("P1", body);
        Assert.Contains("Parking 1", body);
        Assert.Contains("Basement", body);
        Assert.Contains("Anna &lt;A&gt;", body);
        Assert.DoesNotContain("Anna <A>", body);
    }

    [Fact]
    public void Body_has_link_when_url_set() =>
        Assert.Contains("href=\"https://booking.example.org\"",
            WaitlistEmailComposer.Compose(Anna, Tuesday, P1, "https://booking.example.org").HtmlBody);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Body_has_no_link_when_url_empty(string? url) =>
        Assert.DoesNotContain("href", WaitlistEmailComposer.Compose(Anna, Tuesday, P1, url).HtmlBody);

    [Fact]
    public void To_is_recipient_email() =>
        Assert.Equal("anna@example.com", WaitlistEmailComposer.Compose(Anna, Tuesday, P1, null).To);
}
