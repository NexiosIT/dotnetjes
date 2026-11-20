namespace Booking.Core.Services;

public enum BookingError
{
    OutOfRange,
    Weekend,
    ResourceTaken,
    AlreadyBookedThatType,
    PersonInactive,
    ResourceInactive,
    NotFound,
    NotYourBooking
}
