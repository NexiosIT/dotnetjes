using Booking.Core.Services;
using Booking.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.Core.Tests.Bookings;

[Collection("postgres")]
public sealed class CancelTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Cancel_removes_own_booking()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today);

        var result = await ctx.Service.CancelAsync(ctx.Person("Anna"), booked.BookingId!.Value);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        var day = await ctx.Service.GetDayAsync(ctx.Today);
        Assert.Null(day.Desks.Single(s => s.Code == "D2").BookingId);
        Assert.Empty(day.PeopleInOffice);
    }

    [Fact]
    public async Task Cancelled_slot_can_be_booked_again()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today);
        await ctx.Service.CancelAsync(ctx.Person("Anna"), booked.BookingId!.Value);

        var again = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D2"), ctx.Today);

        Assert.True(again.Success);
    }

    [Fact]
    public async Task Cancel_rejects_other_persons()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today);

        var result = await ctx.Service.CancelAsync(ctx.Person("Ben"), booked.BookingId!.Value);

        AssertFailed(result, BookingError.NotYourBooking);
        await AssertBookingCount(ctx, 1);
    }

    [Fact]
    public async Task Cancel_unknown_booking()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var result = await ctx.Service.CancelAsync(ctx.Person("Anna"), 9999);

        AssertFailed(result, BookingError.NotFound);
    }

    [Fact]
    public async Task Cancel_twice_second_is_not_found()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today);
        await ctx.Service.CancelAsync(ctx.Person("Anna"), booked.BookingId!.Value);

        var result = await ctx.Service.CancelAsync(ctx.Person("Anna"), booked.BookingId!.Value);

        AssertFailed(result, BookingError.NotFound);
    }

    [Fact]
    public async Task Cancel_rejects_past_booking()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var bookingId = await ctx.InsertBooking("Anna", "D2", new DateOnly(2026, 11, 17));

        var result = await ctx.Service.CancelAsync(ctx.Person("Anna"), bookingId);

        AssertFailed(result, BookingError.OutOfRange);
        await AssertBookingCount(ctx, 1);
    }

    [Fact]
    public async Task Cancel_other_persons_past_booking_is_not_yours()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var bookingId = await ctx.InsertBooking("Anna", "D2", new DateOnly(2026, 11, 17));

        var result = await ctx.Service.CancelAsync(ctx.Person("Ben"), bookingId);

        AssertFailed(result, BookingError.NotYourBooking);
    }

    private static async Task AssertBookingCount(BookingTestContext ctx, int expected)
    {
        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        Assert.Equal(expected, await db.Bookings.CountAsync());
    }

    private static void AssertFailed(BookingResult result, BookingError expected)
    {
        Assert.False(result.Success);
        Assert.Equal(expected, result.Error);
        Assert.Null(result.BookingId);
    }
}
