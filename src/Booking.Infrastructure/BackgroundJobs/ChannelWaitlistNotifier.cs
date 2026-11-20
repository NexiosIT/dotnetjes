using System.Threading.Channels;
using Booking.Application.Waitlist;

namespace Booking.Infrastructure.BackgroundJobs;

/// <summary>
/// Queues "charger spot freed" messages in memory for <see cref="WaitlistNotificationService"/>.
/// Never blocks or throws: when the queue is full the new message is dropped and logged.
/// Messages still queued are lost on restart (accepted in the waitlist design).
/// </summary>
public sealed class ChannelWaitlistNotifier : IWaitlistNotifier
{
    public const int Capacity = 1000;

    private readonly Channel<ChargerSpotFreed> _channel;

    public ChannelWaitlistNotifier(ILogger<ChannelWaitlistNotifier> logger) =>
        _channel = Channel.CreateBounded<ChargerSpotFreed>(
            new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true },
            dropped => logger.LogWarning("Waitlist queue full; no email for charger spot {ResourceId} on {Date}", dropped.ResourceId, dropped.Date));

    public ChannelReader<ChargerSpotFreed> Reader => _channel.Reader;

    public void ChargerSpotFreed(ChargerSpotFreed freed) => _channel.Writer.TryWrite(freed);
}
