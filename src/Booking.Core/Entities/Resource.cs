namespace Booking.Core.Entities;

/// <summary>A bookable desk or parking spot.</summary>
public class Resource
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public ResourceType Type { get; set; }
    public bool IsActive { get; set; } = true;
}
