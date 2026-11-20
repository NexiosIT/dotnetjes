using Booking.Core.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace Booking.Core.Tests.Notifications;

public sealed class DailySummarySenderTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    private static readonly Person[] People =
    [
        new() { Id = 1, Name = "Anna", Email = "anna@example.com" },
        new() { Id = 2, Name = "Ben", Email = "ben@example.com" },
        new() { Id = 3, Name = "Chris", Email = "chris@example.com", IsActive = false },
        new() { Id = 4, Name = "Dana", Email = null },
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
