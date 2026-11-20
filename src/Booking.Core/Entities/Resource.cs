namespace Booking.Core.Entities;

/// <summary>A bookable desk or parking spot.</summary>
public class Resource
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public ResourceType Type { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Parking only; null for desks or when not set yet.</summary>
    public ParkingLocation? Location { get; set; }

    /// <summary>Parking only: the spot has an EV charge port.</summary>
    public bool HasCharger { get; set; }
}
