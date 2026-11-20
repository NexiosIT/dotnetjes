using Booking.Core.Tests.Infrastructure;

namespace Booking.Core.Tests.Bookings;

[Collection("postgres")]
public sealed class GetDayTests(PostgresFixture postgres)
{
    [Fact]
    public async Task GetDay_lists_active_resources_ordered_by_code()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var day = await ctx.Service.GetDayAsync(ctx.Today);

        Assert.Equal(ctx.Today, day.Date);
        Assert.Equal(8, day.Desks.Count);
        Assert.Equal("D1", day.Desks[0].Code);
        Assert.Equal(["D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8"], day.Desks.Select(s => s.Code));
        Assert.Equal(4, day.Parking.Count);
        Assert.Equal(["P1", "P2", "P3", "P4"], day.Parking.Select(s => s.Code));
        Assert.All(day.Desks.Concat(day.Parking), s => Assert.Null(s.BookingId));
        Assert.Empty(day.PeopleInOffice);
    }

    [Fact]
    public async Task GetDay_shows_bookings_and_distinct_people_in_office()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "D2", ctx.Today);
        await ctx.InsertBooking("Anna", "P2", ctx.Today);

        var day = await ctx.Service.GetDayAsync(ctx.Today);

        var d2 = day.Desks.Single(s => s.Code == "D2");
        Assert.NotNull(d2.BookingId);
        Assert.Equal(ctx.Person("Anna"), d2.BookedByPersonId);
        Assert.Equal("Anna", d2.BookedByName);
        Assert.Equal("Anna", day.Parking.Single(s => s.Code == "P2").BookedByName);
        Assert.Equal(["Anna"], day.PeopleInOffice);
    }

    [Fact]
    public async Task GetDay_hides_inactive_free_resource_keeps_inactive_booked_one()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Ben", "D3", ctx.Today);
        await ctx.DeactivateResource("D3");
        await ctx.DeactivateResource("D4");

        var day = await ctx.Service.GetDayAsync(ctx.Today);

        Assert.Equal("Ben", day.Desks.Single(s => s.Code == "D3").BookedByName);
        Assert.DoesNotContain(day.Desks, s => s.Code == "D4");
    }

    [Fact]
    public async Task GetDay_includes_parking_location_and_charger()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);

        var day = await ctx.Service.GetDayAsync(ctx.Today);

        var p1 = day.Parking.Single(s => s.Code == "P1");
        Assert.Equal(ParkingLocation.Basement, p1.Location);
        Assert.True(p1.HasCharger);
        var p4 = day.Parking.Single(s => s.Code == "P4");
        Assert.Equal(ParkingLocation.BehindTheBuilding, p4.Location);
        Assert.False(p4.HasCharger);
        Assert.All(day.Desks, d => Assert.Null(d.Location));
    }

    [Fact]
    public async Task GetDay_only_shows_bookings_for_that_date()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "D2", ctx.Today.AddDays(1));

        var day = await ctx.Service.GetDayAsync(ctx.Today);

        Assert.Null(day.Desks.Single(s => s.Code == "D2").BookingId);
        Assert.Empty(day.PeopleInOffice);
    }

    [Fact]
    public async Task GetDay_marks_inactive_slots()
    {
        var ctx = await BookingTestContext.CreateAsync(postgres);
        await ctx.InsertBooking("Anna", "P1", ctx.Today);
        await ctx.DeactivateResource("P1");

        var day = await ctx.Service.GetDayAsync(ctx.Today);

        Assert.False(day.Parking.Single(s => s.Code == "P1").IsActive);
        Assert.True(day.Parking.Single(s => s.Code == "P3").IsActive);
    }
}
