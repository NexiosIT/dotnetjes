using Booking.Core.Entities;
using Booking.Core.Services;

namespace Booking.Web.Stubs;

/// <summary>
/// In-memory stand-in so the day view can be built before the real BookingService lands.
/// Only enforces "one booking per resource per day". Deleted in task A4.
/// </summary>
public sealed class StubBookingService : IBookingService
{
    private readonly Lock _lock = new();
    private readonly List<(int Id, int PersonId, int ResourceId, DateOnly Date)> _bookings = [];
    private int _nextId = 1;

    public Task<DayOverview> GetDayAsync(DateOnly date, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var slots = StubData.Resources
                .OrderBy(r => r.Code)
                .Select(r =>
                {
                    var booking = _bookings.FirstOrDefault(b => b.ResourceId == r.Id && b.Date == date);
                    var person = StubData.People.FirstOrDefault(p => p.Id == booking.PersonId);
                    return new ResourceSlot(r.Id, r.Code, r.Name, r.Type,
                        booking.Id == 0 ? null : booking.Id,
                        person?.Id,
                        person?.Name);
                })
                .ToList();

            var people = slots.Where(s => s.BookedByName is not null).Select(s => s.BookedByName!).Distinct().ToList();

            return Task.FromResult(new DayOverview(
                date,
                slots.Where(s => s.Type == ResourceType.Desk).ToList(),
                slots.Where(s => s.Type == ResourceType.Parking).ToList(),
                people));
        }
    }

    public Task<BookingResult> BookAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_bookings.Any(b => b.ResourceId == resourceId && b.Date == date))
            {
                return Task.FromResult(BookingResult.Fail(BookingError.ResourceTaken));
            }

            var id = _nextId++;
            _bookings.Add((id, personId, resourceId, date));
            return Task.FromResult(BookingResult.Ok(id));
        }
    }

    public Task<BookingResult> CancelAsync(int personId, int bookingId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var index = _bookings.FindIndex(b => b.Id == bookingId);
            if (index < 0)
            {
                return Task.FromResult(BookingResult.Fail(BookingError.NotFound));
            }
            if (_bookings[index].PersonId != personId)
            {
                return Task.FromResult(BookingResult.Fail(BookingError.NotYourBooking));
            }

            _bookings.RemoveAt(index);
            return Task.FromResult(BookingResult.Ok(bookingId));
        }
    }
}
