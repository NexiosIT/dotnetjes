using Booking.IntegrationTests.Bookings;
using Booking.IntegrationTests.Infrastructure;
using Booking.Application.Waitlist;
using Microsoft.EntityFrameworkCore;

namespace Booking.IntegrationTests.Waitlist;

[Collection("postgres")]
public sealed class WaitlistServiceTests(PostgresFixture postgres)
{
    private static WaitlistService Service(BookingTestContext ctx) => new(ctx.UnitsOfWork, ctx.Calendar, ctx.Time);

    private static async Task<int> Rows(BookingTestContext ctx)
    {
        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        return await db.Waitlist.CountAsync();
    }

    private static async Task SetEmail(BookingTestContext ctx, string name, string? email)
    {
        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        await db.People.Where(p => p.Name == name).ExecuteUpdateAsync(s => s.SetProperty(p => p.Email, email));
    }

    private static async Task<BookingTestContext> AllChargersTaken(PostgresFixture postgres)
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Ben", "P1", ctx.Today);
        await ctx.InsertBooking("Chris", "P3", ctx.Today);
        return ctx;
    }

    [Fact]
    public async Task Join_succeeds_when_all_charger_spots_taken()
    {
        var ctx = await AllChargersTaken(postgres);
        var waitlist = Service(ctx);

        var result = await waitlist.JoinAsync(ctx.Person("Anna"), ctx.Today);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.True(await waitlist.IsOnWaitlistAsync(ctx.Person("Anna"), ctx.Today));
    }

    [Fact]
    public async Task Join_refused_while_a_charger_spot_is_free()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Ben", "P1", ctx.Today);

        var result = await Service(ctx).JoinAsync(ctx.Person("Anna"), ctx.Today);

        Assert.Equal(WaitlistError.ChargerSpotFree, result.Error);
        Assert.Equal(0, await Rows(ctx));
    }

    [Fact]
    public async Task Join_ignores_inactive_free_charger_spot()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Ben", "P1", ctx.Today);
        await ctx.DeactivateResource("P3");

        var result = await Service(ctx).JoinAsync(ctx.Person("Anna"), ctx.Today);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Join_refused_for_person_with_charger_spot()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "P1", ctx.Today);
        await ctx.InsertBooking("Ben", "P3", ctx.Today);

        var result = await Service(ctx).JoinAsync(ctx.Person("Anna"), ctx.Today);

        Assert.Equal(WaitlistError.AlreadyHasCharger, result.Error);
    }

    [Fact]
    public async Task Join_refused_without_email()
    {
        var ctx = await AllChargersTaken(postgres);
        await SetEmail(ctx, "Anna", null);

        var result = await Service(ctx).JoinAsync(ctx.Person("Anna"), ctx.Today);

        Assert.Equal(WaitlistError.NoEmail, result.Error);
    }

    [Fact]
    public async Task Join_refused_for_inactive_person()
    {
        var ctx = await AllChargersTaken(postgres);
        await ctx.DeactivatePerson("Anna");

        var result = await Service(ctx).JoinAsync(ctx.Person("Anna"), ctx.Today);

        Assert.Equal(WaitlistError.PersonInactive, result.Error);
    }

    [Fact]
    public async Task Join_refused_on_weekend()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var result = await Service(ctx).JoinAsync(ctx.Person("Anna"), new DateOnly(2026, 11, 21));

        Assert.Equal(WaitlistError.Weekend, result.Error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(20)]
    public async Task Join_refused_out_of_range(int days)
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var result = await Service(ctx).JoinAsync(ctx.Person("Anna"), ctx.Today.AddDays(days));

        Assert.Equal(WaitlistError.OutOfRange, result.Error);
    }

    [Fact]
    public async Task Join_unknown_person_is_NotFound()
    {
        var ctx = await AllChargersTaken(postgres);

        var result = await Service(ctx).JoinAsync(999_999, ctx.Today);

        Assert.Equal(WaitlistError.NotFound, result.Error);
    }

    [Fact]
    public async Task Join_twice_is_fine_one_entry()
    {
        var ctx = await AllChargersTaken(postgres);
        var waitlist = Service(ctx);

        var first = await waitlist.JoinAsync(ctx.Person("Anna"), ctx.Today);
        var second = await waitlist.JoinAsync(ctx.Person("Anna"), ctx.Today);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(1, await Rows(ctx));
    }

    [Fact]
    public async Task Concurrent_joins_create_one_entry()
    {
        var ctx = await AllChargersTaken(postgres);

        var results = await Task.WhenAll(
            Service(ctx).JoinAsync(ctx.Person("Anna"), ctx.Today),
            Service(ctx).JoinAsync(ctx.Person("Anna"), ctx.Today));

        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(1, await Rows(ctx));
    }

    [Fact]
    public async Task Can_join_several_days()
    {
        var ctx = await AllChargersTaken(postgres);
        var tomorrow = ctx.Today.AddDays(1);
        await ctx.InsertBooking("Ben", "P1", tomorrow);
        await ctx.InsertBooking("Chris", "P3", tomorrow);
        var waitlist = Service(ctx);

        await waitlist.JoinAsync(ctx.Person("Anna"), ctx.Today);
        await waitlist.JoinAsync(ctx.Person("Anna"), tomorrow);

        Assert.Equal(2, await Rows(ctx));
    }

    [Fact]
    public async Task Leave_removes_entry_and_is_fine_when_absent()
    {
        var ctx = await AllChargersTaken(postgres);
        var waitlist = Service(ctx);
        await waitlist.JoinAsync(ctx.Person("Anna"), ctx.Today);

        await waitlist.LeaveAsync(ctx.Person("Anna"), ctx.Today);
        await waitlist.LeaveAsync(ctx.Person("Anna"), ctx.Today);

        Assert.False(await waitlist.IsOnWaitlistAsync(ctx.Person("Anna"), ctx.Today));
        Assert.Equal(0, await Rows(ctx));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task HasEmail_reflects_person_email(string? missing)
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var waitlist = Service(ctx);
        Assert.True(await waitlist.HasEmailAsync(ctx.Person("Anna")));

        await SetEmail(ctx, "Anna", missing);

        Assert.False(await waitlist.HasEmailAsync(ctx.Person("Anna")));
    }
}
