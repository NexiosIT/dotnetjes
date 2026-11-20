using Booking.Core.Entities;
using Booking.Core.Services;

namespace Booking.Web.Components.Pages;

/// <summary>What the Book page shows: free spots and your own booking, optionally narrowed by location and charger.</summary>
public static class SlotFilter
{
    public static IReadOnlyList<ResourceSlot> Apply(
        IEnumerable<ResourceSlot> slots,
        int? currentPersonId,
        ParkingLocation? location = null,
        bool chargerOnly = false) =>
        slots
            .Where(s => s.BookingId is null || (currentPersonId is not null && s.BookedByPersonId == currentPersonId))
            .Where(s => location is null || s.Location == location)
            .Where(s => !chargerOnly || s.HasCharger)
            .ToList();
}
