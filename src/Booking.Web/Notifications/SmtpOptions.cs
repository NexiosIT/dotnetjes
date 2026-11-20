namespace Booking.Web.Notifications;

/// <summary>Bound from the "Smtp" config section. No auth, no TLS (Mailpit in the demo).</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string From { get; set; } = "booking@example.com";
}
