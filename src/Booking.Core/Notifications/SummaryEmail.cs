namespace Booking.Core.Notifications;

public sealed record SummaryEmail(string To, string Subject, string HtmlBody);
