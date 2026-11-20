namespace Booking.Application.Abstractions;

/// <summary>
/// Starts one unit of work per operation. Blazor Server circuits live for minutes, so use cases never hold
/// a unit of work longer than the call that needs it.
/// </summary>
public interface IUnitOfWorkFactory
{
    Task<IUnitOfWork> CreateAsync(CancellationToken ct = default);
}

/// <summary>
/// The repositories of one operation and their changes, saved together. Disposing without
/// <see cref="CommitAsync"/> rolls back a transaction started with <see cref="BeginTransactionAsync"/>.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IPersonRepository People { get; }
    IResourceRepository Resources { get; }
    IBookingRepository Bookings { get; }
    IWaitlistRepository Waitlist { get; }

    /// <summary>
    /// Saves the pending changes. When a simultaneous request got there first, the database's uniqueness rules refuse
    /// the change and the conflict comes back as a value; nothing is saved then.
    /// </summary>
    Task<SaveConflict?> SaveChangesAsync(CancellationToken ct = default);

    Task BeginTransactionAsync(CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);
}

/// <summary>Which uniqueness rule refused a save: the database decides races that both passed the pre-checks.</summary>
public enum SaveConflict
{
    /// <summary>Someone else booked that desk or spot for that day.</summary>
    ResourceTaken,

    /// <summary>The person already has a desk (or parking spot) that day.</summary>
    AlreadyBookedThatType,

    /// <summary>Another resource already has that code.</summary>
    ResourceCodeTaken,

    /// <summary>A person with that Keycloak id already exists.</summary>
    ExternalIdTaken,

    /// <summary>The person is already on that day's waitlist.</summary>
    AlreadyOnWaitlist
}
