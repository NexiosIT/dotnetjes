using Booking.Application.Bookings;
using Booking.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.IntegrationTests.Bookings;

[Collection("postgres")]
public sealed class BookConflictTests(PostgresFixture postgres)
{
    private static readonly string[] Everyone = ["Anna", "Ben", "Chris", "Dana", "Eli", "Fien"];

    [Fact]
    public async Task GivenD2IsBookedByAnna_WhenBenBooksD2_ThenResourceTaken()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        Assert.True((await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today)).Success);

        var result = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D2"), ctx.Today);

        AssertFailed(result, BookingError.ResourceTaken);
    }

    [Fact]
    public async Task GivenBenHasADesk_WhenBenBooksASecondDeskThatDay_ThenAlreadyBookedThatType()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        Assert.True((await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D3"), ctx.Today)).Success);

        var result = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D4"), ctx.Today);

        AssertFailed(result, BookingError.AlreadyBookedThatType);
    }

    [Fact]
    public async Task GivenBenHasAParkingSpot_WhenBenBooksASecondOneThatDay_ThenAlreadyBookedThatType()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        Assert.True((await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("P1"), ctx.Today)).Success);

        var result = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("P2"), ctx.Today);

        AssertFailed(result, BookingError.AlreadyBookedThatType);
    }

    [Fact]
    public async Task GivenAnnaBookedD2_WhenAnnaBooksD2Again_ThenResourceTaken()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        Assert.True((await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today)).Success);

        var result = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today);

        AssertFailed(result, BookingError.ResourceTaken);
    }

    [Fact]
    public async Task GivenBenHasADesk_WhenBenBooksAParkingSpotThatDay_ThenBothSucceed()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        Assert.True((await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D3"), ctx.Today)).Success);
        Assert.True((await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("P1"), ctx.Today)).Success);
    }

    [Fact]
    public async Task GivenAnnaBookedD2Today_WhenBenBooksD2Tomorrow_ThenItSucceeds()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        Assert.True((await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D2"), ctx.Today)).Success);
        Assert.True((await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D2"), ctx.Today.AddDays(1))).Success);
    }

    [Fact]
    public async Task GivenSixPeople_WhenAllBookDeskD1AtOnce_ThenOneWinsAndFiveGetResourceTaken()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var results = await Task.WhenAll(Everyone.Select(name =>
            Task.Run(() => ctx.Service.BookAsync(ctx.Person(name), ctx.Resource("D1"), ctx.Today))));

        Assert.Single(results, r => r.Success);
        Assert.Equal(5, results.Count(r => r.Error == BookingError.ResourceTaken));
        await AssertBookingCount(ctx, 1);
    }

    [Fact]
    public async Task GivenAnna_WhenBookingSixDesksAtOnce_ThenOneSucceedsAndFiveGetAlreadyBookedThatType()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        string[] desks = ["D1", "D2", "D3", "D4", "D5", "D6"];

        var results = await Task.WhenAll(desks.Select(code =>
            Task.Run(() => ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource(code), ctx.Today))));

        Assert.Single(results, r => r.Success);
        Assert.Equal(5, results.Count(r => r.Error == BookingError.AlreadyBookedThatType));
        await AssertBookingCount(ctx, 1);
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
