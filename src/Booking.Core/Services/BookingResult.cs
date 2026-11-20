namespace Booking.Core.Services;

public sealed record BookingResult(bool Success, BookingError? Error, int? BookingId)
{
    public static BookingResult Ok(int bookingId) => new(true, null, bookingId);
    public static BookingResult Fail(BookingError error) => new(false, error, null);
}
