namespace Booking.Application.Abstractions;

public sealed record SummaryEmail(string To, string Subject, string HtmlBody);
