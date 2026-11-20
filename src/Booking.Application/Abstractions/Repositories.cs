namespace Booking.Application.Abstractions;

/// <summary>People, as tracked entities: changes are saved by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
public interface IPersonRepository
{
    Task<Person?> GetAsync(int id, CancellationToken ct = default);

    Task<Person?> FindByExternalIdAsync(string externalId, CancellationToken ct = default);

    /// <summary>Everyone, ordered by name.</summary>
    Task<IReadOnlyList<Person>> ListAsync(CancellationToken ct = default);

    void Add(Person person);
}

/// <summary>Desks and parking spots, as tracked entities.</summary>
public interface IResourceRepository
{
    Task<Resource?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Desks first, then parking, each ordered by code.</summary>
    Task<IReadOnlyList<Resource>> ListAsync(CancellationToken ct = default);

    Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct = default);

    /// <summary>An active charger spot nobody booked for <paramref name="date"/>.</summary>
    Task<bool> AnyFreeChargerSpotAsync(DateOnly date, CancellationToken ct = default);

    void Add(Resource resource);
}

public interface IBookingRepository
{
    /// <summary>The booking with its resource, not tracked.</summary>
    Task<Booking.Domain.Bookings.Booking?> GetWithResourceAsync(int id, CancellationToken ct = default);

    /// <summary>The person's desk (or parking) booking for that day, with its resource, not tracked.</summary>
    Task<Booking.Domain.Bookings.Booking?> FindAsync(int personId, ResourceType type, DateOnly date, CancellationToken ct = default);

    Task<bool> IsResourceTakenAsync(int resourceId, DateOnly date, CancellationToken ct = default);

    Task<bool> HasBookingOfTypeAsync(int personId, ResourceType type, DateOnly date, CancellationToken ct = default);

    /// <summary>The person holds a parking spot with a charger that day.</summary>
    Task<bool> HasChargerSpotAsync(int personId, DateOnly date, CancellationToken ct = default);

    void Add(Booking.Domain.Bookings.Booking booking);

    /// <summary>
    /// Deletes the person's booking straight away (not on save) and reports whether it was still there:
    /// the same person may have removed it in another tab a moment earlier.
    /// </summary>
    Task<bool> DeleteAsync(int bookingId, int personId, CancellationToken ct = default);
}

public interface IWaitlistRepository
{
    Task<bool> ExistsAsync(int personId, DateOnly date, CancellationToken ct = default);

    void Add(WaitlistEntry entry);

    /// <summary>Removes the person's entry for that day straight away (not on save); not on the list is fine.</summary>
    Task RemoveAsync(int personId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// Who to email about a charger spot freed on <paramref name="date"/>: people on that day's list who are active,
    /// have an email address, aren't <paramref name="exceptPersonId"/> and don't hold a charger spot that day by now.
    /// </summary>
    Task<IReadOnlyList<Person>> WaitersToNotifyAsync(DateOnly date, int exceptPersonId, CancellationToken ct = default);
}
