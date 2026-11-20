using Booking.Core.Services;

namespace Booking.Web.Components.Pages;

/// <summary>Which charger waitlist line the Book page shows under the parking list.</summary>
public enum ChargerWaitlistLine
{
    None,
    CanJoin,
    OnList,
    NeedsEmail
}

/// <summary>Book page decisions for the charger waitlist and "Switch here", kept out of the markup so they can be tested.</summary>
public static class ChargerWaitlist
{
    /// <summary>
    /// A line only when the ⚡ Charger filter is on, every active charger spot is taken
    /// and the person doesn't hold one themselves.
    /// </summary>
    public static ChargerWaitlistLine For(DayOverview day, int? personId, bool chargerFilterOn, bool onWaitlist, bool hasEmail)
    {
        var chargers = day.Parking.Where(s => s.HasCharger).ToList();
        if (!chargerFilterOn
            || personId is null
            || chargers.Any(s => s.IsActive && s.BookingId is null)
            || chargers.Any(s => s.BookedByPersonId == personId))
        {
            return ChargerWaitlistLine.None;
        }
        return onWaitlist ? ChargerWaitlistLine.OnList
            : hasEmail ? ChargerWaitlistLine.CanJoin
            : ChargerWaitlistLine.NeedsEmail;
    }

    /// <summary>True when the person already has a desk (or parking spot) that day: other free ones then offer "Switch here".</summary>
    public static bool HasBookingOfType(DayOverview day, int? personId, ResourceType type) =>
        personId is not null
        && (type == ResourceType.Desk ? day.Desks : day.Parking).Any(s => s.BookedByPersonId == personId);
}
