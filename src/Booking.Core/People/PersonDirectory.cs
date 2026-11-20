using Booking.Core.Data;
using Booking.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Core.People;

public sealed class PersonDirectory(IDbContextFactory<BookingDbContext> dbFactory) : IPersonDirectory
{
    public async Task<Person> SignInAsync(string externalId, string name, string? email, CancellationToken ct = default)
    {
        var trimmedName = name.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        if (await TryRefreshAsync(externalId, trimmedName, normalizedEmail, ct) is { } existing)
        {
            return existing;
        }

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var person = new Person { ExternalId = externalId, Name = trimmedName, Email = normalizedEmail };
            db.People.Add(person);
            await db.SaveChangesAsync(ct);
            return person;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: BookingDbContext.PersonExternalIdIndex
        })
        {
            // Another request created this person between our lookup and insert: use theirs.
            return await TryRefreshAsync(externalId, trimmedName, normalizedEmail, ct)
                ?? throw new InvalidOperationException($"Person with external id '{externalId}' vanished during sign-in.", ex);
        }
    }

    private async Task<Person?> TryRefreshAsync(string externalId, string name, string? email, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var person = await db.People.SingleOrDefaultAsync(p => p.ExternalId == externalId, ct);
        if (person is null)
        {
            return null;
        }

        person.Name = name;
        person.Email = email;
        await db.SaveChangesAsync(ct);
        return person;
    }
}
