using Booking.Core.Tests.Bookings;
using Booking.Core.Tests.Infrastructure;
using Booking.Core.Tests.Notifications;
using Booking.Core.Waitlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Booking.Core.Tests.Waitlist;

[Collection("postgres")]
public sealed class WaitlistEmailSenderTests(PostgresFixture postgres)
{
    private static WaitlistEmailSender Sender(BookingTestContext ctx, RecordingEmailSender emails) =>
        new(ctx.DbFactory, emails, new WaitlistEmailSettings("https://booking.example.org"), NullLogger<WaitlistEmailSender>.Instance);

    private static async Task Waiting(BookingTestContext ctx, DateOnly date, params string[] names)
    {
        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        foreach (var name in names)
        {
            db.Waitlist.Add(new WaitlistEntry { PersonId = ctx.Person(name), Date = date, CreatedAt = BookingTestContext.Now.UtcDateTime });
        }
        await db.SaveChangesAsync();
    }

    private static ChargerSpotFreed Freed(BookingTestContext ctx, string code, string by) =>
        new(ctx.Today, ctx.Resource(code), ctx.Person(by));

    [Fact]
    public async Task Emails_waiters_for_that_date_only()
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
    public async Task Skips_person_who_freed_the_spot()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await Waiting(ctx, ctx.Today, "Anna", "Ben");
        var emails = new RecordingEmailSender();

        await Sender(ctx, emails).SendAsync(Freed(ctx, "P1", "Ben"), CancellationToken.None);

        Assert.Equal(["anna@example.com"], emails.Sent.Select(e => e.To));
    }

    [Fact]
    public async Task Skips_inactive_and_emailless_waiters()
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
    public async Task Skips_waiter_who_now_holds_a_charger_spot()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await Waiting(ctx, ctx.Today, "Anna", "Chris");
        await ctx.InsertBooking("Chris", "P3", ctx.Today);
        var emails = new RecordingEmailSender();

        await Sender(ctx, emails).SendAsync(Freed(ctx, "P1", "Ben"), CancellationToken.None);

        Assert.Equal(["anna@example.com"], emails.Sent.Select(e => e.To));
    }

    [Fact]
    public async Task One_failing_address_does_not_stop_others()
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
    public async Task Inactive_or_non_charger_spot_sends_nothing()
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
