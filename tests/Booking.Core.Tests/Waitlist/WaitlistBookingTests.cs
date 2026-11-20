using Booking.Core.Entities;
using Booking.Core.Services;
using Booking.Core.Tests.Bookings;
using Booking.Core.Tests.Infrastructure;
using Booking.Core.Waitlist;
using Microsoft.EntityFrameworkCore;

namespace Booking.Core.Tests.Waitlist;

[Collection("postgres")]
public sealed class WaitlistBookingTests(PostgresFixture postgres)
{
    private static async Task OnWaitlist(BookingTestContext ctx, string name, DateOnly date)
    {
        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        db.Waitlist.Add(new WaitlistEntry { PersonId = ctx.Person(name), Date = date, CreatedAt = BookingTestContext.Now.UtcDateTime });
        await db.SaveChangesAsync();
    }

    private static async Task<bool> IsWaiting(BookingTestContext ctx, string name, DateOnly date)
    {
        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        return await db.Waitlist.AnyAsync(w => w.PersonId == ctx.Person(name) && w.Date == date);
    }

    [Fact]
    public async Task Booking_a_charger_spot_removes_waitlist_entry()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await OnWaitlist(ctx, "Anna", ctx.Today);

        var result = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        Assert.True(result.Success);
        Assert.False(await IsWaiting(ctx, "Anna", ctx.Today));
    }

    [Fact]
    public async Task Booking_an_ordinary_spot_keeps_waitlist_entry()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await OnWaitlist(ctx, "Anna", ctx.Today);

        await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("P2"), ctx.Today);

        Assert.True(await IsWaiting(ctx, "Anna", ctx.Today));
    }

    [Fact]
    public async Task Booking_charger_spot_keeps_entries_for_other_days()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var tomorrow = ctx.Today.AddDays(1);
        await OnWaitlist(ctx, "Anna", ctx.Today);
        await OnWaitlist(ctx, "Anna", tomorrow);

        await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        Assert.True(await IsWaiting(ctx, "Anna", tomorrow));
    }

    [Fact]
    public async Task Failed_charger_booking_keeps_waitlist_entry()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Ben", "P1", ctx.Today);
        await OnWaitlist(ctx, "Anna", ctx.Today);

        var result = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        Assert.Equal(BookingError.ResourceTaken, result.Error);
        Assert.True(await IsWaiting(ctx, "Anna", ctx.Today));
    }

    [Fact]
    public async Task Cancelling_charger_spot_notifies()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("P1"), ctx.Today);

        await ctx.Service.CancelAsync(ctx.Person("Ben"), booked.BookingId!.Value);

        Assert.Equal([new ChargerSpotFreed(ctx.Today, ctx.Resource("P1"), ctx.Person("Ben"))], ctx.Notifier.Freed);
    }

    [Theory]
    [InlineData("P2")]
    [InlineData("D1")]
    public async Task Cancelling_ordinary_spot_or_desk_does_not_notify(string code)
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource(code), ctx.Today);

        await ctx.Service.CancelAsync(ctx.Person("Ben"), booked.BookingId!.Value);

        Assert.Empty(ctx.Notifier.Freed);
    }

    [Fact]
    public async Task Failed_cancel_does_not_notify()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("P1"), ctx.Today);

        var result = await ctx.Service.CancelAsync(ctx.Person("Anna"), booked.BookingId!.Value);

        Assert.Equal(BookingError.NotYourBooking, result.Error);
        Assert.Empty(ctx.Notifier.Freed);
    }

    [Fact]
    public async Task Cancelling_inactive_charger_spot_does_not_notify()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("P1"), ctx.Today);
        await ctx.DeactivateResource("P1");

        await ctx.Service.CancelAsync(ctx.Person("Ben"), booked.BookingId!.Value);

        Assert.Empty(ctx.Notifier.Freed);
    }
}
