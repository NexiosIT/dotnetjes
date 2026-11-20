using Booking.Application.Waitlist;
using Booking.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Booking.IntegrationTests.BackgroundJobs;

public class ChannelWaitlistNotifierTests
{
    private static readonly ChargerSpotFreed Freed = new(new DateOnly(2026, 11, 24), 1, 2);

    [Fact]
    public void GivenANotifier_WhenAChargerSpotIsFreed_ThenTheEventIsReadableFromTheReader()
    {
        var notifier = new ChannelWaitlistNotifier(NullLogger<ChannelWaitlistNotifier>.Instance);

        notifier.ChargerSpotFreed(Freed);

        Assert.True(notifier.Reader.TryRead(out var read));
        Assert.Equal(Freed, read);
    }

    [Fact]
    public void GivenAFullQueue_WhenAChargerSpotIsFreed_ThenItNeitherBlocksNorThrowsAndTheQueueStaysAtCapacity()
    {
        var notifier = new ChannelWaitlistNotifier(NullLogger<ChannelWaitlistNotifier>.Instance);

        for (var i = 0; i < ChannelWaitlistNotifier.Capacity + 1; i++)
        {
            notifier.ChargerSpotFreed(Freed with { ResourceId = i });
        }

        Assert.Equal(ChannelWaitlistNotifier.Capacity, notifier.Reader.Count);
        Assert.Equal(1000, ChannelWaitlistNotifier.Capacity);
    }
}
