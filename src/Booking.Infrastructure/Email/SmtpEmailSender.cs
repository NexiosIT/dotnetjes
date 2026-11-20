using Booking.Application.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Booking.Infrastructure.Email;

/// <summary>One SMTP connection per email, so one bad address or a dropped connection only fails that email.</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(SummaryEmail email, CancellationToken ct)
    {
        var smtp = options.Value;
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp.From));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.Body = new TextPart("html") { Text = email.HtmlBody };

        using var client = new SmtpClient { Timeout = 10_000 };
        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.None, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
