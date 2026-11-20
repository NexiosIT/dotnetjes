using Booking.Application.Bookings;

namespace Booking.Web.Components.Pages;

/// <summary>Plain-language text shown above the day view when a Book or Cancel fails. Owned by Person B — task B3.</summary>
public static class BookingMessages
{
    public static string For(BookingError error, ResourceSlot slot) => error switch
    {
        BookingError.OutOfRange => "That day can't be booked.",
        BookingError.Weekend => "The office is closed at weekends.",
        BookingError.ResourceTaken => $"{slot.Code} was just taken — pick another one.",
        BookingError.AlreadyBookedThatType => slot.Type == ResourceType.Desk
            ? "You already have a desk that day."
            : "You already have a parking spot that day.",
        BookingError.PersonInactive => "Your profile is inactive — ask an admin.",
        BookingError.ResourceInactive => $"{slot.Code} is no longer available.",
        BookingError.NotFound => "That no longer exists — the page has been refreshed.",
        BookingError.NotYourBooking => "You can only cancel your own bookings.",
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null)
    };
}
