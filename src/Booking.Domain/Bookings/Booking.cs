namespace Booking.Domain.Bookings;

public class Booking
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public int ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;

    /// <summary>Copied from the resource so the database can enforce one desk and one parking spot per person per day.</summary>
    public ResourceType ResourceType { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>Always UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
