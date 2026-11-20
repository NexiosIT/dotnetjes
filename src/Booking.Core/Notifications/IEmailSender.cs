namespace Booking.Core.Notifications;

public interface IEmailSender
{
    Task SendAsync(SummaryEmail email, CancellationToken ct);
}
