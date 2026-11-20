using Booking.Application.Bookings;
using Booking.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.IntegrationTests.Bookings;

[Collection("postgres")]
public sealed class CancelTests(PostgresFixture postgres)
{
    [Fact]
    public async Task GivenAnnaBookedD2_WhenAnnaCancels_ThenItSucceedsAndD2IsFreeAndNobodyIsInTheOffice()
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
    public async Task GivenAnnaCancelledD2_WhenBenBooksD2_ThenItSucceeds()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today);
        await ctx.Service.CancelAsync(ctx.Person("Anna"), booked.BookingId!.Value);

        var again = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D2"), ctx.Today);

        Assert.True(again.Success);
    }

    [Fact]
    public async Task GivenAnnaBookedD2_WhenBenCancelsIt_ThenNotYourBookingAndTheBookingRemains()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today);

        var result = await ctx.Service.CancelAsync(ctx.Person("Ben"), booked.BookingId!.Value);

        AssertFailed(result, BookingError.NotYourBooking);
        await AssertBookingCount(ctx, 1);
    }

    [Fact]
    public async Task WhenCancellingAnUnknownBooking_ThenNotFound()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var result = await ctx.Service.CancelAsync(ctx.Person("Anna"), 9999);

        AssertFailed(result, BookingError.NotFound);
    }

    [Fact]
    public async Task GivenAnnaCancelledHerBooking_WhenAnnaCancelsItAgain_ThenNotFound()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var booked = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today);
        await ctx.Service.CancelAsync(ctx.Person("Anna"), booked.BookingId!.Value);

        var result = await ctx.Service.CancelAsync(ctx.Person("Anna"), booked.BookingId!.Value);

        AssertFailed(result, BookingError.NotFound);
    }

    [Fact]
    public async Task GivenABookingInThePast_WhenAnnaCancelsIt_ThenOutOfRangeAndTheBookingRemains()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        var bookingId = await ctx.InsertBooking("Anna", "D2", new DateOnly(2026, 11, 17));

        var result = await ctx.Service.CancelAsync(ctx.Person("Anna"), bookingId);

        AssertFailed(result, BookingError.OutOfRange);
        await AssertBookingCount(ctx, 1);
    }

    [Fact]
    public async Task GivenAnnasBookingInThePast_WhenBenCancelsIt_ThenNotYourBooking()
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
