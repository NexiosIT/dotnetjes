using System.Globalization;
using System.Net;
using System.Text;
using Booking.Core.Notifications;

namespace Booking.Core.Waitlist;

/// <summary>Where the waitlist email links to (<c>App:PublicUrl</c>). Empty → no link.</summary>
public sealed record WaitlistEmailSettings(string? PublicUrl);

/// <summary>Builds the "a charger spot is free" email for one person on the waitlist.</summary>
public static class WaitlistEmailComposer
{
    public static SummaryEmail Compose(Person recipient, DateOnly date, Resource spot, string? publicUrl)
    {
        var dateText = date.ToString("ddd d MMM", CultureInfo.InvariantCulture);
        var subject = $"A charger spot is free on {dateText}";
        var where = spot.Location is ParkingLocation location ? $" ({ParkingLocations.Label(location)})" : "";

        var body = new StringBuilder();
        body.Append($"<p>Hi {Encode(recipient.Name)},</p>");
        body.Append($"<p>{Encode(spot.Code)} – {Encode(spot.Name)}{Encode(where)} has just become free on {Encode(dateText)}.</p>");
        body.Append("<p>Whoever books it first gets it.</p>");
        if (!string.IsNullOrWhiteSpace(publicUrl))
        {
            body.Append($"<p><a href=\"{Encode(publicUrl.Trim())}\">Open the booking app</a></p>");
        }
        body.Append("<p>You're on the waitlist for that day and will get another email if another charger spot frees up.</p>");

        return new SummaryEmail(recipient.Email ?? "", subject, body.ToString());
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
