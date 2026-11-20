using Booking.Application.Bookings;
using Booking.IntegrationTests.Infrastructure;
using Booking.Application.Waitlist;
using Microsoft.EntityFrameworkCore;

namespace Booking.IntegrationTests.Bookings;

[Collection("postgres")]
public sealed class SwitchTests(PostgresFixture postgres)
{
    private static async Task<string?> Holder(BookingTestContext ctx, string code, DateOnly date)
    {
        var day = await ctx.Service.GetDayAsync(date);
        return day.Desks.Concat(day.Parking).Single(s => s.Code == code).BookedByName;
    }

    [Fact]
    public async Task Switch_moves_parking_booking()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "P2", ctx.Today);

        var result = await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        Assert.True(result.Success);
        Assert.NotNull(result.BookingId);
        Assert.Equal("Anna", await Holder(ctx, "P1", ctx.Today));
        Assert.Null(await Holder(ctx, "P2", ctx.Today));
    }

    [Fact]
    public async Task Switch_moves_desk_booking()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "D1", ctx.Today);

        var result = await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today);

        Assert.True(result.Success);
        Assert.Equal("Anna", await Holder(ctx, "D2", ctx.Today));
        Assert.Null(await Holder(ctx, "D1", ctx.Today));
    }

    [Fact]
    public async Task Switch_onto_own_spot_is_noop()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var id = await ctx.InsertBooking("Anna", "P2", ctx.Today);

        var result = await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P2"), ctx.Today);

        Assert.Equal(BookingResult.Ok(id), result);
        Assert.Empty(ctx.Notifier.Freed);
    }

    [Fact]
    public async Task Switch_without_existing_booking_is_NotFound()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "D1", ctx.Today);

        var result = await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        Assert.Equal(BookingError.NotFound, result.Error);
        Assert.Null(await Holder(ctx, "P1", ctx.Today));
    }

    [Fact]
    public async Task Switch_to_taken_spot_keeps_old_booking()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Ben", "P1", ctx.Today);
        await ctx.InsertBooking("Anna", "P2", ctx.Today);

        var result = await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        Assert.Equal(BookingError.ResourceTaken, result.Error);
        Assert.Equal("Anna", await Holder(ctx, "P2", ctx.Today));
        Assert.Equal("Ben", await Holder(ctx, "P1", ctx.Today));
    }

    [Fact]
    public async Task Concurrent_switches_onto_same_spot_one_wins_loser_keeps_old()
    {
        for (var round = 0; round < 5; round++)
        {
            var ctx = await BookingTestContext.CreateAsync(postgres);
            await ctx.InsertBooking("Anna", "P2", ctx.Today);
            await ctx.InsertBooking("Ben", "P4", ctx.Today);

            var results = await Task.WhenAll(
                ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today),
                ctx.Service.SwitchAsync(ctx.Person("Ben"), ctx.Resource("P1"), ctx.Today));

            Assert.Single(results, r => r.Success);
            Assert.Single(results, r => r.Error == BookingError.ResourceTaken);
            var winner = results[0].Success ? "Anna" : "Ben";
            var (loser, loserSpot) = winner == "Anna" ? ("Ben", "P4") : ("Anna", "P2");
            Assert.Equal(winner, await Holder(ctx, "P1", ctx.Today));
            Assert.Equal(loser, await Holder(ctx, loserSpot, ctx.Today));
        }
    }

    [Fact]
    public async Task Switch_validation_matches_book()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "P2", ctx.Today);

        Assert.Equal(BookingError.Weekend,
            (await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P1"), new DateOnly(2026, 11, 21))).Error);
        Assert.Equal(BookingError.OutOfRange,
            (await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today.AddDays(-1))).Error);
        Assert.Equal(BookingError.NotFound,
            (await ctx.Service.SwitchAsync(ctx.Person("Anna"), 999_999, ctx.Today)).Error);

        await ctx.DeactivateResource("P1");
        Assert.Equal(BookingError.ResourceInactive,
            (await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today)).Error);

        await ctx.DeactivatePerson("Anna");
        Assert.Equal(BookingError.PersonInactive,
            (await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P3"), ctx.Today)).Error);
        Assert.Equal("Anna", await Holder(ctx, "P2", ctx.Today));
    }

    [Fact]
    public async Task Switch_onto_charger_removes_waitlist_entry()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "P2", ctx.Today);
        await using (var db = await ctx.DbFactory.CreateDbContextAsync())
        {
            db.Waitlist.Add(WaitlistEntry.Reconstitute(ctx.Person("Anna"), ctx.Today, BookingTestContext.Now.UtcDateTime));
            await db.SaveChangesAsync();
        }

        await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        await using var check = await ctx.DbFactory.CreateDbContextAsync();
        Assert.False(await check.Waitlist.AnyAsync());
    }

    [Fact]
    public async Task Switch_away_from_charger_notifies()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "P1", ctx.Today);

        await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P2"), ctx.Today);

        Assert.Equal([new ChargerSpotFreed(ctx.Today, ctx.Resource("P1"), ctx.Person("Anna"))], ctx.Notifier.Freed);
    }

    [Fact]
    public async Task Switch_between_ordinary_spots_does_not_notify()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "P2", ctx.Today);

        await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P4"), ctx.Today);

        Assert.Empty(ctx.Notifier.Freed);
    }

    [Fact]
    public async Task Failed_switch_away_from_charger_does_not_notify()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "P1", ctx.Today);
        await ctx.InsertBooking("Ben", "P2", ctx.Today);

        await ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P2"), ctx.Today);

        Assert.Empty(ctx.Notifier.Freed);
    }

    /// <summary>
    /// Same person, two tabs: one cancels (or switches) while the other is switching. The other tab holds the row lock
    /// on the old booking until it commits; the switch must then answer NotFound instead of crashing the page.
    /// </summary>
    [Fact]
    public async Task Old_booking_removed_mid_switch_is_NotFound_not_a_crash()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var old = await ctx.InsertBooking("Anna", "P2", ctx.Today);

        await using var other = await ctx.DbFactory.CreateDbContextAsync();
        await using var otherTab = await other.Database.BeginTransactionAsync();
        await other.Bookings.Where(b => b.Id == old).ExecuteDeleteAsync();

        var switching = ctx.Service.SwitchAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);
        await Task.Delay(500);
        await otherTab.CommitAsync();
        var result = await switching;

        Assert.Equal(BookingError.NotFound, result.Error);
        Assert.Null(await Holder(ctx, "P1", ctx.Today));
    }
}
