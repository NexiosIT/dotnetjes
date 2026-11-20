namespace Booking.Application.Waitlist;

/// <summary>
/// Told when a charger spot frees up, after the change is saved. Implementations must return at once and never throw:
/// the emails go out in the background, so a slow mail server never holds up a Cancel or Switch.
/// </summary>
public interface IWaitlistNotifier
{
    void ChargerSpotFreed(ChargerSpotFreed freed);
}

/// <summary>Tells nobody.</summary>
public sealed class NoWaitlistNotifier : IWaitlistNotifier
{
    public void ChargerSpotFreed(ChargerSpotFreed freed)
    {
    }
}
