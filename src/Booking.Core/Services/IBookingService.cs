namespace Booking.Core.Services;

public interface IBookingService
{
    Task<DayOverview> GetDayAsync(DateOnly date, CancellationToken ct = default);
    Task<BookingResult> BookAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default);
    Task<BookingResult> CancelAsync(int personId, int bookingId, CancellationToken ct = default);
}
