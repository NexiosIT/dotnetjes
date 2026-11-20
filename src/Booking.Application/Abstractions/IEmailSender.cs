namespace Booking.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(SummaryEmail email, CancellationToken ct);
}
