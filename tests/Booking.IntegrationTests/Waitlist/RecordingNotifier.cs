using Booking.Application.Waitlist;

namespace Booking.IntegrationTests.Waitlist;

/// <summary>Remembers every "charger spot freed" signal.</summary>
public sealed class RecordingNotifier : IWaitlistNotifier
{
    public List<ChargerSpotFreed> Freed { get; } = [];

    public void ChargerSpotFreed(ChargerSpotFreed freed)
    {
        lock (Freed)
        {
            Freed.Add(freed);
        }
    }
}
