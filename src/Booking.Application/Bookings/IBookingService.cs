namespace Booking.Application.Bookings;

public interface IBookingService
{
    Task<DayOverview> GetDayAsync(DateOnly date, CancellationToken ct = default);
    Task<BookingResult> BookAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default);
    Task<BookingResult> CancelAsync(int personId, int bookingId, CancellationToken ct = default);

    /// <summary>
    /// Moves the person's booking of that type (desk or parking) on <paramref name="date"/> to <paramref name="resourceId"/>, in one step.
    /// If the new spot is taken first, the old booking stays. No booking of that type → NotFound.
    /// </summary>
    Task<BookingResult> SwitchAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default);
}
