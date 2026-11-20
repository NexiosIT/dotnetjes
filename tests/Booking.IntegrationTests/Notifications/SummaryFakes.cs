using Booking.Application.Admin;
using Booking.Application.Notifications;
using Booking.Application.Bookings;

namespace Booking.IntegrationTests.Notifications;

/// <summary>Builds a day with desks D1–D8 and parking P1–P4; bookings are (code, person id, person name).</summary>
internal static class Days
{
    public static DayOverview With(DateOnly date, params (string Code, int PersonId, string Name)[] bookings)
    {
        var desks = Enumerable.Range(1, 8).Select(i => Slot($"D{i}", i, ResourceType.Desk, bookings)).ToList();
        var parking = Enumerable.Range(1, 4).Select(i => Slot($"P{i}", 100 + i, ResourceType.Parking, bookings)).ToList();
        var people = bookings.Select(b => b.Name).Distinct().ToList();
        return new DayOverview(date, desks, parking, people);
    }

    private static ResourceSlot Slot(string code, int id, ResourceType type, (string Code, int PersonId, string Name)[] bookings)
    {
        var booking = bookings.SingleOrDefault(b => b.Code == code);
        return booking.Code is null
            ? new ResourceSlot(id, code, code, type, null, null, null)
            : new ResourceSlot(id, code, code, type, 1000 + id, booking.PersonId, booking.Name);
    }
}

internal sealed class FakeBookingService(DayOverview day) : IBookingService
{
    public List<DateOnly> RequestedDates { get; } = [];

    public Task<DayOverview> GetDayAsync(DateOnly date, CancellationToken ct = default)
    {
        RequestedDates.Add(date);
        return Task.FromResult(day);
    }

    public Task<BookingResult> BookAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<BookingResult> SwitchAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<BookingResult> CancelAsync(int personId, int bookingId, CancellationToken ct = default) =>
        throw new NotSupportedException();
}

internal sealed class FakeAdminService(params PersonView[] people) : IAdminService
{
    public Task<IReadOnlyList<PersonView>> GetPeopleAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PersonView>>(people);

    public Task<IReadOnlyList<ResourceView>> GetResourcesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ResourceView> AddResourceAsync(string code, string name, ResourceType type, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateResourceAsync(int id, string code, string name, bool isActive, CancellationToken ct = default) => throw new NotSupportedException();
    public Task SetParkingDetailsAsync(int id, ParkingLocation? location, bool hasCharger, CancellationToken ct = default) => throw new NotSupportedException();
    public Task SetPersonActiveAsync(int id, bool isActive, CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class FixedCalendar(DateOnly today) : IOfficeCalendar
{
    public DateOnly Today => today;
    public DateOnly FirstBookableDay => today;
    public DateOnly LastBookableDay => today.AddDays(14);
    public bool IsBookable(DateOnly date) => throw new NotSupportedException();
    public DateOnly NextWeekday(DateOnly date) => throw new NotSupportedException();
    public DateOnly PreviousWeekday(DateOnly date) => throw new NotSupportedException();
}

/// <summary>Records every email; throws for the addresses in <paramref name="failFor"/>.</summary>
internal sealed class RecordingEmailSender(params string[] failFor) : IEmailSender
{
    public List<SummaryEmail> Sent { get; } = [];

    public Task SendAsync(SummaryEmail email, CancellationToken ct)
    {
        if (failFor.Contains(email.To))
        {
            throw new InvalidOperationException($"Mailbox {email.To} unavailable");
        }
        Sent.Add(email);
        return Task.CompletedTask;
    }
}
