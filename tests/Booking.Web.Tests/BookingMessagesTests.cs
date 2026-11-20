using Booking.Application.Bookings;
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
    public void GivenAParkingSlot_WhenGettingTheMessageForAnError_ThenThePlannedCopyIsReturned(BookingError error, string expected) =>
        Assert.Equal(expected, BookingMessages.For(error, Slot("P2", ResourceType.Parking)));

    [Fact]
    public void GivenADeskSlot_WhenGettingTheAlreadyBookedThatTypeMessage_ThenItSaysDesk() =>
        Assert.Equal("You already have a desk that day.",
            BookingMessages.For(BookingError.AlreadyBookedThatType, Slot("D4", ResourceType.Desk)));

    [Fact]
    public void WhenGettingTheMessageForEveryBookingError_ThenNoneIsBlank()
    {
        foreach (var error in Enum.GetValues<BookingError>())
            Assert.False(string.IsNullOrWhiteSpace(BookingMessages.For(error, Slot("D1", ResourceType.Desk))));
    }
}
