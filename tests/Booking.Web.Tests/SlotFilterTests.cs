using Booking.Application.Bookings;
using Booking.Web.Components.Pages;

namespace Booking.Web.Tests;

public sealed class SlotFilterTests
{
    private const int Anna = 1;
    private const int Ben = 2;

    private static ResourceSlot Spot(string code, ParkingLocation? location = null, bool charger = false, int? bookedBy = null) =>
        new(code.GetHashCode(), code, code, ResourceType.Parking,
            bookedBy is null ? null : 100, bookedBy, bookedBy is null ? null : $"Person {bookedBy}",
            location, charger);

    private static readonly ResourceSlot[] Spots =
    [
        Spot("P1", ParkingLocation.Basement, charger: true),
        Spot("P2", ParkingLocation.Basement, bookedBy: Ben),
        Spot("P3", ParkingLocation.Outside, charger: true, bookedBy: Anna),
        Spot("P4", ParkingLocation.BehindTheBuilding),
        Spot("P5"),
    ];

    private static string[] Codes(IEnumerable<ResourceSlot> slots) => slots.Select(s => s.Code).ToArray();

    [Fact]
    public void Hides_spots_taken_by_someone_else_keeps_own()
    {
        Assert.Equal(["P1", "P3", "P4", "P5"], Codes(SlotFilter.Apply(Spots, Anna)));
    }

    [Fact]
    public void Nobody_picked_sees_only_free_spots()
    {
        Assert.Equal(["P1", "P4", "P5"], Codes(SlotFilter.Apply(Spots, currentPersonId: null)));
    }

    [Fact]
    public void Filters_on_location()
    {
        Assert.Equal(["P1"], Codes(SlotFilter.Apply(Spots, Anna, ParkingLocation.Basement)));
        Assert.Equal(["P3"], Codes(SlotFilter.Apply(Spots, Anna, ParkingLocation.Outside)));
    }

    [Fact]
    public void Filters_on_charger()
    {
        Assert.Equal(["P1", "P3"], Codes(SlotFilter.Apply(Spots, Anna, chargerOnly: true)));
    }

    [Fact]
    public void Combines_location_and_charger()
    {
        Assert.Equal(["P3"], Codes(SlotFilter.Apply(Spots, Anna, ParkingLocation.Outside, chargerOnly: true)));
        Assert.Empty(SlotFilter.Apply(Spots, Anna, ParkingLocation.BehindTheBuilding, chargerOnly: true));
    }
}
