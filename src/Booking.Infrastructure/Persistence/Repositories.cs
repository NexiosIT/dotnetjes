using Microsoft.EntityFrameworkCore;
using BookingEntity = Booking.Domain.Bookings.Booking;

namespace Booking.Infrastructure.Persistence;

internal sealed class PersonRepository(BookingDbContext db) : IPersonRepository
{
    public Task<Person?> GetAsync(int id, CancellationToken ct = default) =>
        db.People.SingleOrDefaultAsync(p => p.Id == id, ct);

    public Task<Person?> FindByExternalIdAsync(string externalId, CancellationToken ct = default) =>
        db.People.SingleOrDefaultAsync(p => p.ExternalId == externalId, ct);

    public async Task<IReadOnlyList<Person>> ListAsync(CancellationToken ct = default) =>
        await db.People.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public void Add(Person person) => db.People.Add(person);
}

internal sealed class ResourceRepository(BookingDbContext db) : IResourceRepository
{
    public Task<Resource?> GetAsync(int id, CancellationToken ct = default) =>
        db.Resources.SingleOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Resource>> ListAsync(CancellationToken ct = default) =>
        await db.Resources.AsNoTracking().OrderBy(r => r.Type).ThenBy(r => r.Code).ToListAsync(ct);

    public Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct = default) =>
        db.Resources.AnyAsync(r => r.Code == code && r.Id != exceptId, ct);

    public Task<bool> AnyFreeChargerSpotAsync(DateOnly date, CancellationToken ct = default) =>
        db.Resources.AnyAsync(r => r.Type == ResourceType.Parking && r.HasCharger && r.IsActive
            && !db.Bookings.Any(b => b.ResourceId == r.Id && b.Date == date), ct);

    public void Add(Resource resource) => db.Resources.Add(resource);
}

internal sealed class BookingRepository(BookingDbContext db) : IBookingRepository
{
    public Task<BookingEntity?> GetWithResourceAsync(int id, CancellationToken ct = default) =>
        db.Bookings.AsNoTracking().Include(b => b.Resource).SingleOrDefaultAsync(b => b.Id == id, ct);

    public Task<BookingEntity?> FindAsync(int personId, ResourceType type, DateOnly date, CancellationToken ct = default) =>
        db.Bookings.AsNoTracking().Include(b => b.Resource)
            .SingleOrDefaultAsync(b => b.PersonId == personId && b.ResourceType == type && b.Date == date, ct);

    public Task<bool> IsResourceTakenAsync(int resourceId, DateOnly date, CancellationToken ct = default) =>
        db.Bookings.AnyAsync(b => b.ResourceId == resourceId && b.Date == date, ct);

    public Task<bool> HasBookingOfTypeAsync(int personId, ResourceType type, DateOnly date, CancellationToken ct = default) =>
        db.Bookings.AnyAsync(b => b.PersonId == personId && b.ResourceType == type && b.Date == date, ct);

    public Task<bool> HasChargerSpotAsync(int personId, DateOnly date, CancellationToken ct = default) =>
        db.Bookings.AnyAsync(b => b.PersonId == personId && b.Date == date
            && b.Resource.Type == ResourceType.Parking && b.Resource.HasCharger, ct);

    public void Add(BookingEntity booking) => db.Bookings.Add(booking);

    public async Task<bool> DeleteAsync(int bookingId, int personId, CancellationToken ct = default) =>
        await db.Bookings.Where(b => b.Id == bookingId && b.PersonId == personId).ExecuteDeleteAsync(ct) == 1;
}

internal sealed class WaitlistRepository(BookingDbContext db) : IWaitlistRepository
{
    public Task<bool> ExistsAsync(int personId, DateOnly date, CancellationToken ct = default) =>
        db.Waitlist.AnyAsync(w => w.PersonId == personId && w.Date == date, ct);

    public void Add(WaitlistEntry entry) => db.Waitlist.Add(entry);

    public Task RemoveAsync(int personId, DateOnly date, CancellationToken ct = default) =>
        db.Waitlist.Where(w => w.PersonId == personId && w.Date == date).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<Person>> WaitersToNotifyAsync(DateOnly date, int exceptPersonId, CancellationToken ct = default) =>
        await db.Waitlist.AsNoTracking()
            .Where(w => w.Date == date && w.PersonId != exceptPersonId)
            .Select(w => w.Person)
            .Where(p => p.IsActive && p.Email != null && p.Email.Trim() != "")
            .Where(p => !db.Bookings.Any(b => b.PersonId == p.Id && b.Date == date
                && b.Resource.Type == ResourceType.Parking && b.Resource.HasCharger))
            .ToListAsync(ct);
}
