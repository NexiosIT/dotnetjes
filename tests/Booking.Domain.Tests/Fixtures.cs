using Microsoft.Extensions.Time.Testing;

namespace Booking.Domain.Tests;

/// <summary>Office calendar frozen at Wed 2026-11-18 09:00 Brussels: bookable from today to Wed 2026-12-02.</summary>
internal static class Fixtures
{
    public static readonly DateOnly Today = new(2026, 11, 18);
    public static readonly DateOnly Saturday = new(2026, 11, 21);
    public static readonly DateTime Now = new(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc);

    public static IOfficeCalendar Calendar { get; } = new OfficeCalendar(
        new FakeTimeProvider(new DateTimeOffset(Now)), TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels"), maxDaysAhead: 14);

    public static Person Anna(string? email = "anna@example.com") => Person.FromLogin("kc-anna", "Anna", email);

    public static Resource Desk(string code = "D1") => Resource.Create(code, $"Desk {code}", ResourceType.Desk);

    public static Resource ChargerSpot(string code = "P1")
    {
        var spot = Resource.Create(code, $"Parking {code}", ResourceType.Parking);
        spot.SetParkingDetails(ParkingLocation.Basement, hasCharger: true);
        return spot;
    }
}
