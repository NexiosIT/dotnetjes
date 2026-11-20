namespace Booking.Domain.Bookings;

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
