namespace Booking.Core.Entities;

public class Person
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
}
