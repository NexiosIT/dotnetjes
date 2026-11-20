namespace Booking.Application.Bookings;

/// <param name="PeopleInOffice">Distinct names of everyone with any booking that day.</param>
public sealed record DayOverview(
    DateOnly Date,
    IReadOnlyList<ResourceSlot> Desks,
    IReadOnlyList<ResourceSlot> Parking,
    IReadOnlyList<string> PeopleInOffice);
