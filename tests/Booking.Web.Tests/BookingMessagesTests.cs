using Booking.Core.Entities;
using Booking.Core.Services;
using Booking.Web.Components.Pages;

namespace Booking.Web.Tests;

public class BookingMessagesTests
{
    private static ResourceSlot Slot(string code, ResourceType type) =>
        new(1, code, code, type, null, null, null);

    [Theory]
    [InlineData(BookingError.OutOfRange, "That day can't be booked.")]
    [InlineData(BookingError.Weekend, "The office is closed at weekends.")]
    [InlineData(BookingError.ResourceTaken, "P2 was just taken — pick another one.")]
    [InlineData(BookingError.AlreadyBookedThatType, "You already have a parking spot that day.")]
    [InlineData(BookingError.PersonInactive, "Your profile is inactive — ask an admin.")]
    [InlineData(BookingError.ResourceInactive, "P2 is no longer available.")]
    [InlineData(BookingError.NotFound, "That no longer exists — the page has been refreshed.")]
    [InlineData(BookingError.NotYourBooking, "You can only cancel your own bookings.")]
    public void For_parking_slot_returns_plan_copy(BookingError error, string expected) =>
        Assert.Equal(expected, BookingMessages.For(error, Slot("P2", ResourceType.Parking)));

    [Fact]
    public void AlreadyBookedThatType_on_desk_says_desk() =>
        Assert.Equal("You already have a desk that day.",
            BookingMessages.For(BookingError.AlreadyBookedThatType, Slot("D4", ResourceType.Desk)));

    [Fact]
    public void Every_error_has_a_message()
    {
        foreach (var error in Enum.GetValues<BookingError>())
            Assert.False(string.IsNullOrWhiteSpace(BookingMessages.For(error, Slot("D1", ResourceType.Desk))));
    }
}
