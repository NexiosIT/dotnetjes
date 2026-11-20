using Booking.Core.Entities;
using Booking.Core.Services;
using Booking.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.Core.Tests.Bookings;

[Collection("postgres")]
public sealed class BookValidationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Book_succeeds_and_stores_type_and_utc_timestamp()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var result = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("P1"), ctx.Today);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.NotNull(result.BookingId);
        Assert.True(result.BookingId > 0);

        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        var stored = await db.Bookings.SingleAsync(b => b.Id == result.BookingId);
        Assert.Equal(ctx.Person("Anna"), stored.PersonId);
        Assert.Equal(ctx.Resource("P1"), stored.ResourceId);
        Assert.Equal(ctx.Today, stored.Date);
        Assert.Equal(ResourceType.Parking, stored.ResourceType);
        Assert.Equal(new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc), stored.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);
    }

    [Fact]
    public async Task Book_succeeds_on_last_bookable_day()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var result = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D1"), new DateOnly(2026, 12, 2));

        Assert.True(result.Success);
    }

    [Theory]
    [InlineData("2026-11-17")] // yesterday
    [InlineData("2026-12-03")] // today + 15
    public async Task Book_rejects_out_of_range(string date)
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var result = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D1"), DateOnly.Parse(date));

        AssertFailed(result, BookingError.OutOfRange);
    }

    [Fact]
    public async Task Book_rejects_weekend()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var result = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D1"), new DateOnly(2026, 11, 21));

        AssertFailed(result, BookingError.Weekend);
    }

    [Fact]
    public async Task Book_rejects_inactive_person()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.DeactivatePerson("Ben");

        var result = await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D1"), ctx.Today);

        AssertFailed(result, BookingError.PersonInactive);
    }

    [Fact]
    public async Task Book_rejects_inactive_resource()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.DeactivateResource("D5");

        var result = await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D5"), ctx.Today);

        AssertFailed(result, BookingError.ResourceInactive);
    }

    [Fact]
    public async Task Book_rejects_unknown_ids()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        AssertFailed(await ctx.Service.BookAsync(9999, ctx.Resource("D1"), ctx.Today), BookingError.NotFound);
        AssertFailed(await ctx.Service.BookAsync(ctx.Person("Anna"), 9999, ctx.Today), BookingError.NotFound);
    }

    [Fact]
    public async Task Book_checks_date_before_active_state()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.DeactivatePerson("Ben");
        await ctx.DeactivateResource("D5");

        AssertFailed(await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D5"), new DateOnly(2026, 11, 17)), BookingError.OutOfRange);
        AssertFailed(await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D5"), new DateOnly(2026, 11, 21)), BookingError.Weekend);
        AssertFailed(await ctx.Service.BookAsync(ctx.Person("Ben"), ctx.Resource("D5"), ctx.Today), BookingError.PersonInactive);
    }

    [Fact]
    public async Task Rejected_booking_stores_nothing()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.DeactivateResource("D5");

        await ctx.Service.BookAsync(ctx.Person("Anna"), ctx.Resource("D5"), ctx.Today);

        await using var db = await ctx.DbFactory.CreateDbContextAsync();
        Assert.Equal(0, await db.Bookings.CountAsync());
    }

    private static void AssertFailed(BookingResult result, BookingError expected)
    {
        Assert.False(result.Success);
        Assert.Equal(expected, result.Error);
        Assert.Null(result.BookingId);
    }
}
