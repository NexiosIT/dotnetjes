using System.Globalization;
using System.Net;
using System.Text;
using Booking.Core.Services;

namespace Booking.Core.Notifications;

/// <summary>Builds the "who's in the office today" email for one recipient.</summary>
public static class DailySummaryComposer
{
    public static SummaryEmail Compose(DayOverview day, Person recipient)
    {
        var dateText = day.Date.ToString("ddd d MMM", CultureInfo.InvariantCulture);
        var subject = $"Who's in the office – {dateText}";

        var body = new StringBuilder();
        body.Append($"<p>Hi {Encode(recipient.Name)},</p>");
        body.Append($"<h2>{Encode(subject)}</h2>");

        var booked = day.Desks.Concat(day.Parking).Where(s => s.BookingId is not null).ToList();
        if (booked.Count == 0)
        {
            body.Append("<p>Nobody has booked for today.</p>");
        }
        else
        {
            body.Append("<table cellpadding=\"4\"><tr><th align=\"left\">Who</th><th align=\"left\">Desk</th><th align=\"left\">Parking</th></tr>");
            foreach (var person in booked.GroupBy(s => (s.BookedByPersonId, s.BookedByName)).OrderBy(g => g.Key.BookedByName, StringComparer.OrdinalIgnoreCase))
            {
                var desk = person.FirstOrDefault(s => s.Type == ResourceType.Desk)?.Code ?? "–";
                var parking = person.FirstOrDefault(s => s.Type == ResourceType.Parking)?.Code ?? "–";
                body.Append($"<tr><td>{Encode(person.Key.BookedByName ?? "")}</td><td>{Encode(desk)}</td><td>{Encode(parking)}</td></tr>");
            }
            body.Append("</table>");
        }

        var freeDesks = day.Desks.Count(s => s.BookingId is null);
        var freeParking = day.Parking.Count(s => s.BookingId is null);
        body.Append($"<p>Free: {Plural(freeDesks, "desk", "desks")}, {Plural(freeParking, "parking spot", "parking spots")}</p>");

        return new SummaryEmail(recipient.Email ?? "", subject, body.ToString());
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    private static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
}
