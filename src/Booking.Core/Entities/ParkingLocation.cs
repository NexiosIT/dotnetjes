namespace Booking.Core.Entities;

/// <summary>
/// Where a parking spot is. Stored as text, so adding a location needs no migration:
/// add a value here and its label in <see cref="ParkingLocations.Label"/>.
/// </summary>
public enum ParkingLocation
{
    Outside,
    Basement,
    BehindTheBuilding
}

public static class ParkingLocations
{
    /// <summary>Every location, in the order the UI lists them.</summary>
    public static IReadOnlyList<ParkingLocation> All { get; } = Enum.GetValues<ParkingLocation>();

    public static string Label(ParkingLocation location) => location switch
    {
        ParkingLocation.Outside => "Outside",
        ParkingLocation.Basement => "Basement",
        ParkingLocation.BehindTheBuilding => "Behind the building",
        _ => location.ToString()
    };
}
