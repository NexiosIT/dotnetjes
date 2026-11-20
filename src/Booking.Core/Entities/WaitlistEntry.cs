namespace Booking.Core.Entities;

/// <summary>A person waiting for a charger spot on one day. One entry per person per day.</summary>
public class WaitlistEntry
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public DateOnly Date { get; set; }

    /// <summary>Always UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
