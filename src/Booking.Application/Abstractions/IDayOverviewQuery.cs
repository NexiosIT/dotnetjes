using Booking.Application.Bookings;

namespace Booking.Application.Abstractions;

/// <summary>Read side of the Book page: every slot of a day and who booked it, straight from the database.</summary>
public interface IDayOverviewQuery
{
    /// <summary>Active resources, plus inactive ones that still have a booking that day.</summary>
    Task<DayOverview> GetDayAsync(DateOnly date, CancellationToken ct = default);
}
