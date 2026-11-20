using Booking.Core.Entities;

namespace Booking.Core.Services;

/// <summary>
/// One row in the day view. A day lists active resources, plus inactive ones that still have a booking that day.
/// </summary>
public sealed record ResourceSlot(
    int ResourceId,
    string Code,
    string Name,
    ResourceType Type,
    int? BookingId,
    int? BookedByPersonId,
    string? BookedByName,
    ParkingLocation? Location = null,
    bool HasCharger = false,
    bool IsActive = true);
