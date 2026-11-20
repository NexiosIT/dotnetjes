using Booking.Application.Bookings;
using Booking.IntegrationTests.Bookings;
using Booking.IntegrationTests.Infrastructure;
using Booking.Application.Waitlist;
using Microsoft.EntityFrameworkCore;

namespace Booking.IntegrationTests.Waitlist;

[Collection("postgres")]
public sealed class WaitlistBookingTests(PostgresFixture postgres)
{
    private static async Task OnWaitlist(BookingTestContext ctx, string name, DateOnly date)
    {
        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        db.Waitlist.Add(WaitlistEntry.Reconstitute(ctx.Person(name), date, BookingTestContext.Now.UtcDateTime));
        await db.SaveChangesAsync();
    }

    private static async Task<bool> IsWaiting(BookingTestContext ctx, string name, DateOnly date)
    {
        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        return await db.Waitlist.AnyAsync(w => w.PersonId == ctx.Person(name) && w.Date == date);
    }

    [Fact]
    public async Task GivenAnnaIsOnTheWaitlist_WhenSheBooksAChargerSpot_ThenHerWaitlistEntryIsRemoved()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await OnWaitlist(ctx, "Anna", ctx.Today);

        var result = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        Assert.True(result.Success);
        Assert.False(await IsWaiting(ctx, "Anna", ctx.Today));
    }

    [Fact]
    public async Task GivenAnnaIsOnTheWaitlist_WhenSheBooksAnOrdinarySpot_ThenHerWaitlistEntryIsKept()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await OnWaitlist(ctx, "Anna", ctx.Today);

        await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("P2"), ctx.Today);

        Assert.True(await IsWaiting(ctx, "Anna", ctx.Today));
    }

    [Fact]
    public async Task GivenAnnaWaitsTodayAndTomorrow_WhenSheBooksAChargerSpotToday_ThenTomorrowsEntryIsKept()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var tomorrow = ctx.Today.AddDays(1);
        await OnWaitlist(ctx, "Anna", ctx.Today);
        await OnWaitlist(ctx, "Anna", tomorrow);

        await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        Assert.True(await IsWaiting(ctx, "Anna", tomorrow));
    }

    [Fact]
    public async Task GivenP1IsTakenAndAnnaWaits_WhenAnnaBooksP1_ThenResourceTakenAndHerEntryIsKept()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Ben", "P1", ctx.Today);
        await OnWaitlist(ctx, "Anna", ctx.Today);

        var result = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        Assert.Equal(BookingError.ResourceTaken, result.Error);
        Assert.True(await IsWaiting(ctx, "Anna", ctx.Today));
    }

    [Fact]
    public async Task GivenBenBookedChargerSpotP1_WhenBenCancels_ThenTheFreedChargerSpotIsNotified()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("P1"), ctx.Today);

        await ctx.Service.CancelAsync(ctx.Person("Ben"), booked.BookingId!.Value);

        Assert.Equal([new ChargerSpotFreed(ctx.Today, ctx.Resource("P1"), ctx.Person("Ben"))], ctx.Notifier.Freed);
    }

    [Theory]
    [InlineData("P2")]
    [InlineData("D1")]
    public async Task GivenBenBookedAnOrdinarySpotOrDesk_WhenBenCancels_ThenNobodyIsNotified(string code)
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource(code), ctx.Today);

        await ctx.Service.CancelAsync(ctx.Person("Ben"), booked.BookingId!.Value);

        Assert.Empty(ctx.Notifier.Freed);
    }

    [Fact]
    public async Task GivenBenBookedP1_WhenAnnaCancelsIt_ThenNotYourBookingAndNobodyIsNotified()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("P1"), ctx.Today);

        var result = await ctx.Service.CancelAsync(ctx.Person("Anna"), booked.BookingId!.Value);

        Assert.Equal(BookingError.NotYourBooking, result.Error);
        Assert.Empty(ctx.Notifier.Freed);
    }

    [Fact]
    public async Task GivenChargerSpotP1BecameInactive_WhenBenCancelsIt_ThenNobodyIsNotified()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("P1"), ctx.Today);
        await ctx.DeactivateResource("P1");

        await ctx.Service.CancelAsync(ctx.Person("Ben"), booked.BookingId!.Value);

        Assert.Empty(ctx.Notifier.Freed);
    }
}
