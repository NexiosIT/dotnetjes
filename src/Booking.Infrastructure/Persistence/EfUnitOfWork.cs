using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Booking.Infrastructure.Persistence;

public sealed class EfUnitOfWorkFactory(IDbContextFactory<BookingDbContext> dbFactory) : IUnitOfWorkFactory
{
    public async Task<IUnitOfWork> CreateAsync(CancellationToken ct = default) =>
        new EfUnitOfWork(await dbFactory.CreateDbContextAsync(ct));
}

/// <summary>One <see cref="BookingDbContext"/> per operation; its unique indexes are translated to <see cref="SaveConflict"/>.</summary>
public sealed class EfUnitOfWork(BookingDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public IPersonRepository People { get; } = new PersonRepository(db);
    public IResourceRepository Resources { get; } = new ResourceRepository(db);
    public IBookingRepository Bookings { get; } = new BookingRepository(db);
    public IWaitlistRepository Waitlist { get; } = new WaitlistRepository(db);

    public async Task<SaveConflict?> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException ex) when (ConflictFrom(ex) is { } conflict)
        {
            db.ChangeTracker.Clear();
            return conflict;
        }
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default) =>
        _transaction = await db.Database.BeginTransactionAsync(ct);

    public Task CommitAsync(CancellationToken ct = default) =>
        _transaction?.CommitAsync(ct) ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }
        await db.DisposeAsync();
    }

    private static SaveConflict? ConflictFrom(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            ? pg.ConstraintName switch
            {
                BookingDbContext.ResourceDateIndex => SaveConflict.ResourceTaken,
                BookingDbContext.PersonTypeDateIndex => SaveConflict.AlreadyBookedThatType,
                BookingDbContext.ResourceCodeIndex => SaveConflict.ResourceCodeTaken,
                BookingDbContext.PersonExternalIdIndex => SaveConflict.ExternalIdTaken,
                BookingDbContext.WaitlistIndex => SaveConflict.AlreadyOnWaitlist,
                _ => null
            }
            : null;
}
