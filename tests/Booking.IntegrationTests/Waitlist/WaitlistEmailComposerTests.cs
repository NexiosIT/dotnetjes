using Booking.Application.Waitlist;

namespace Booking.IntegrationTests.Waitlist;

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
    public void WhenComposingTheWaitlistEmail_ThenTheSubjectNamesTheDay() =>
        Assert.Equal("A charger spot is free on Tue 24 Nov", WaitlistEmailComposer.Compose(Anna, Tuesday, P1, null).Subject);

    [Fact]
    public void WhenComposingTheWaitlistEmail_ThenTheBodyNamesTheSpotAndLocationAndTheNameIsHtmlEncoded()
    {
        var body = WaitlistEmailComposer.Compose(Anna, Tuesday, P1, null).HtmlBody;

        Assert.Contains("P1", body);
        Assert.Contains("Parking 1", body);
        Assert.Contains("Basement", body);
        Assert.Contains("Anna &lt;A&gt;", body);
        Assert.DoesNotContain("Anna <A>", body);
    }

    [Fact]
    public void GivenAUrlIsSet_WhenComposingTheWaitlistEmail_ThenTheBodyHasALink() =>
        Assert.Contains("href=\"https://booking.example.org\"",
            WaitlistEmailComposer.Compose(Anna, Tuesday, P1, "https://booking.example.org").HtmlBody);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GivenTheUrlIsNullOrBlank_WhenComposingTheWaitlistEmail_ThenTheBodyHasNoLink(string? url) =>
        Assert.DoesNotContain("href", WaitlistEmailComposer.Compose(Anna, Tuesday, P1, url).HtmlBody);

    [Fact]
    public void WhenComposingTheWaitlistEmail_ThenItIsAddressedToTheRecipient() =>
        Assert.Equal("anna@example.com", WaitlistEmailComposer.Compose(Anna, Tuesday, P1, null).To);
}
