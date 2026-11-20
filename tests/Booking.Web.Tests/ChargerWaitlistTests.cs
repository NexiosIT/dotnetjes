using Booking.Core.Services;
using Booking.Web.Components.Pages;

namespace Booking.Web.Tests;

public class ChargerWaitlistTests
{
    private const int Anna = 1, Ben = 2, Chris = 3;
    private static readonly DateOnly Date = new(2026, 11, 24);

    /// <summary>Parking: P1 and P3 have chargers, P2 doesn't. Bookings are (code, person).</summary>
    private static DayOverview Day(bool p3Active = true, params (string Code, int Person)[] bookings)
    {
        ResourceSlot Slot(int id, string code, bool charger, bool active = true)
        {
            var booking = bookings.FirstOrDefault(b => b.Code == code);
            return booking.Code is null
                ? new ResourceSlot(id, code, code, ResourceType.Parking, null, null, null, HasCharger: charger, IsActive: active)
                : new ResourceSlot(id, code, code, ResourceType.Parking, 100 + id, booking.Person, $"person {booking.Person}", HasCharger: charger, IsActive: active);
        }

        var desk = bookings.Any(b => b.Code == "D1")
            ? new ResourceSlot(10, "D1", "Desk 1", ResourceType.Desk, 110, bookings.First(b => b.Code == "D1").Person, "x")
            : new ResourceSlot(10, "D1", "Desk 1", ResourceType.Desk, null, null, null);
        return new DayOverview(Date, [desk], [Slot(1, "P1", true), Slot(2, "P2", false), Slot(3, "P3", true, p3Active)], []);
    }

    private static readonly DayOverview AllChargersTaken = Day(true, ("P1", Ben), ("P3", Chris));

    [Fact]
    public void None_when_filter_off() =>
        Assert.Equal(ChargerWaitlistLine.None, ChargerWaitlist.For(AllChargersTaken, Anna, chargerFilterOn: false, onWaitlist: false, hasEmail: true));

    [Fact]
    public void None_when_an_active_charger_spot_is_free() =>
        Assert.Equal(ChargerWaitlistLine.None, ChargerWaitlist.For(Day(true, ("P1", Ben)), Anna, true, false, true));

    [Fact]
    public void None_when_person_holds_a_charger_spot() =>
        Assert.Equal(ChargerWaitlistLine.None, ChargerWaitlist.For(Day(true, ("P1", Anna), ("P3", Ben)), Anna, true, false, true));

    [Fact]
    public void None_without_a_person() =>
        Assert.Equal(ChargerWaitlistLine.None, ChargerWaitlist.For(AllChargersTaken, null, true, false, true));

    [Fact]
    public void CanJoin_when_all_taken_and_not_on_list() =>
        Assert.Equal(ChargerWaitlistLine.CanJoin, ChargerWaitlist.For(AllChargersTaken, Anna, true, false, true));

    [Fact]
    public void OnList_when_on_list() =>
        Assert.Equal(ChargerWaitlistLine.OnList, ChargerWaitlist.For(AllChargersTaken, Anna, true, true, true));

    [Fact]
    public void NeedsEmail_when_no_email() =>
        Assert.Equal(ChargerWaitlistLine.NeedsEmail, ChargerWaitlist.For(AllChargersTaken, Anna, true, false, false));

    [Fact]
    public void Inactive_free_charger_spot_counts_as_taken() =>
        Assert.Equal(ChargerWaitlistLine.CanJoin, ChargerWaitlist.For(Day(false, ("P1", Ben)), Anna, true, false, true));

    [Fact]
    public void True_when_person_has_parking_that_day() =>
        Assert.True(ChargerWaitlist.HasBookingOfType(Day(true, ("P2", Anna)), Anna, ResourceType.Parking));

    [Fact]
    public void False_for_other_type() =>
        Assert.False(ChargerWaitlist.HasBookingOfType(Day(true, ("D1", Anna)), Anna, ResourceType.Parking));

    [Fact]
    public void False_for_null_person() =>
        Assert.False(ChargerWaitlist.HasBookingOfType(Day(true, ("P2", Anna)), null, ResourceType.Parking));
}
