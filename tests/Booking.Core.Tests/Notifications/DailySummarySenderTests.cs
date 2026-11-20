using Booking.Core.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace Booking.Core.Tests.Notifications;

public sealed class DailySummarySenderTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    private static readonly Person[] People =
    [
        Person.Reconstitute(1, "Anna", "anna@example.com"),
        Person.Reconstitute(2, "Ben", "ben@example.com"),
        Person.Reconstitute(3, "Chris", "chris@example.com", isActive: false),
        Person.Reconstitute(4, "Dana", null),
    ];

    private static DailySummarySender CreateSender(IEmailSender emails, FakeBookingService? bookings = null) =>
        new(bookings ?? new FakeBookingService(Days.With(Today)),
            new FakeAdminService(People),
            new FixedCalendar(Today),
            emails,
            NullLogger<DailySummarySender>.Instance);

    [Fact]
    public async Task Send_goes_to_each_active_person_with_email()
    {
        var emails = new RecordingEmailSender();
        var bookings = new FakeBookingService(Days.With(Today));

        var result = await CreateSender(emails, bookings).SendTodayAsync(CancellationToken.None);

        Assert.Equal(new SummaryResult(2, 0), result);
        Assert.Equal(["anna@example.com", "ben@example.com"], emails.Sent.Select(e => e.To).Order());
        Assert.Equal([Today], bookings.RequestedDates);
    }

    [Fact]
    public async Task Send_survives_a_failing_address()
    {
        var emails = new RecordingEmailSender(failFor: "ben@example.com");

        var result = await CreateSender(emails).SendTodayAsync(CancellationToken.None);

        Assert.Equal(new SummaryResult(1, 1), result);
        Assert.Equal("anna@example.com", Assert.Single(emails.Sent).To);
    }
}
