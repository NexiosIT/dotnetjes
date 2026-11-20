using Booking.Core.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Core.People;

public sealed class PersonDirectory(IDbContextFactory<BookingDbContext> dbFactory) : IPersonDirectory
{
    public async Task<SignedInPerson> SignInAsync(string externalId, string name, string? email, CancellationToken ct = default)
    {
        if (await TryRefreshAsync(externalId, name, email, ct) is { } existing)
        {
            return existing;
        }

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var person = Person.FromLogin(externalId, name, email);
            db.People.Add(person);
            await db.SaveChangesAsync(ct);
            return SignedInPerson.From(person);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: BookingDbContext.PersonExternalIdIndex
        })
        {
            return await TryRefreshAsync(externalId, name, email, ct)
                ?? throw new InvalidOperationException($"Person with external id '{externalId}' vanished during sign-in.", ex);
        }
    }

    private async Task<SignedInPerson?> TryRefreshAsync(string externalId, string name, string? email, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var person = await db.People.SingleOrDefaultAsync(p => p.ExternalId == externalId, ct);
        if (person is null)
        {
            return null;
        }

        person.RefreshProfile(name, email);
        await db.SaveChangesAsync(ct);
        return SignedInPerson.From(person);
    }
}
