namespace Booking.Domain.Waitlist;

/// <summary>Domain event: a charger spot lost its booking for <paramref name="Date"/>; <paramref name="FreedByPersonId"/> is who gave it up.</summary>
public sealed record ChargerSpotFreed(DateOnly Date, int ResourceId, int FreedByPersonId);
