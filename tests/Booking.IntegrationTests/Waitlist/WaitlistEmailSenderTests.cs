using Booking.IntegrationTests.Bookings;
using Booking.IntegrationTests.Infrastructure;
using Booking.IntegrationTests.Notifications;
using Booking.Application.Waitlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Booking.IntegrationTests.Waitlist;

[Collection("postgres")]
public sealed class WaitlistEmailSenderTests(PostgresFixture postgres)
{
    private static WaitlistEmailSender Sender(BookingTestContext ctx, RecordingEmailSender emails) =>
        new(ctx.UnitsOfWork, emails, new WaitlistEmailSettings("https://booking.example.org"), NullLogger<WaitlistEmailSender>.Instance);

    private static async Task Waiting(BookingTestContext ctx, DateOnly date, params string[] names)
    {
        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        foreach (var name in names)
        {
            db.Waitlist.Add(WaitlistEntry.Reconstitute(ctx.Person(name), date, BookingTestContext.Now.UtcDateTime));
        }
        await db.SaveChangesAsync();
    }

    private static ChargerSpotFreed Freed(BookingTestContext ctx, string code, string by) =>
        new(ctx.Today, ctx.Resource(code), ctx.Person(by));

    [Fact]
    public async Task GivenWaitersOnTwoDates_WhenASpotIsFreed_ThenOnlyWaitersForThatDateAreEmailed()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await Waiting(ctx, ctx.Today, "Anna", "Chris");
        await Waiting(ctx, ctx.Today.AddDays(1), "Dana");
        var emails = new RecordingEmailSender();

        var result = await Sender(ctx, emails).SendAsync(Freed(ctx, "P1", "Ben"), CancellationToken.None);

        Assert.Equal(["anna@example.com", "chris@example.com"], emails.Sent.Select(e => e.To).Order());
        Assert.All(emails.Sent, e => Assert.Equal("A charger spot is free on Wed 18 Nov", e.Subject));
        Assert.Equal(2, result.Sent);
    }

    [Fact]
    public async Task GivenThePersonWhoFreedTheSpotWaits_WhenSending_ThenThatPersonIsSkipped()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await Waiting(ctx, ctx.Today, "Anna", "Ben");
        var emails = new RecordingEmailSender();

        await Sender(ctx, emails).SendAsync(Freed(ctx, "P1", "Ben"), CancellationToken.None);

        Assert.Equal(["anna@example.com"], emails.Sent.Select(e => e.To));
    }

    [Fact]
    public async Task GivenInactiveAndEmaillessWaiters_WhenSending_ThenTheyAreSkipped()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await Waiting(ctx, ctx.Today, "Anna", "Chris", "Dana");
        await ctx.DeactivatePerson("Chris");
        await using (var db = await ctx.DbFactory.CreateDbContextAsync())
        {
            await db.People.Where(p => p.Name == "Dana").ExecuteUpdateAsync(s => s.SetProperty(p => p.Email, (string?)null));
        }
        var emails = new RecordingEmailSender();

        await Sender(ctx, emails).SendAsync(Freed(ctx, "P1", "Ben"), CancellationToken.None);

        Assert.Equal(["anna@example.com"], emails.Sent.Select(e => e.To));
    }

    [Fact]
    public async Task GivenAWaiterHoldsAChargerSpot_WhenSending_ThenThatWaiterIsSkipped()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await Waiting(ctx, ctx.Today, "Anna", "Chris");
        await ctx.InsertBooking("Chris", "P3", ctx.Today);
        var emails = new RecordingEmailSender();

        await Sender(ctx, emails).SendAsync(Freed(ctx, "P1", "Ben"), CancellationToken.None);

        Assert.Equal(["anna@example.com"], emails.Sent.Select(e => e.To));
    }

    [Fact]
    public async Task GivenOneAddressFails_WhenSending_ThenTheOthersAreStillEmailedAndTheFailureIsCounted()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await Waiting(ctx, ctx.Today, "Anna", "Chris");
        var emails = new RecordingEmailSender(failFor: "anna@example.com");

        var result = await Sender(ctx, emails).SendAsync(Freed(ctx, "P1", "Ben"), CancellationToken.None);

        Assert.Equal(["chris@example.com"], emails.Sent.Select(e => e.To));
        Assert.Equal(1, result.Sent);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task GivenAnOrdinaryOrInactiveSpotIsFreed_WhenSending_ThenNothingIsSent()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await Waiting(ctx, ctx.Today, "Anna");
        var emails = new RecordingEmailSender();

        await Sender(ctx, emails).SendAsync(Freed(ctx, "P2", "Ben"), CancellationToken.None);
        await ctx.DeactivateResource("P1");
        await Sender(ctx, emails).SendAsync(Freed(ctx, "P1", "Ben"), CancellationToken.None);

        Assert.Empty(emails.Sent);
    }
}
