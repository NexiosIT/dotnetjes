namespace Booking.Domain.Waitlist;

public enum WaitlistError
{
    NotFound,
    OutOfRange,
    Weekend,
    PersonInactive,
    NoEmail,
    AlreadyHasCharger,
    ChargerSpotFree
}
