using Booking.Core.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Core.Admin;

/// <summary>
/// Codes are trimmed and upper-cased; names and emails are trimmed; a blank email is stored as null.
/// Nothing is ever deleted: deactivate instead.
/// </summary>
public sealed class AdminService(IDbContextFactory<BookingDbContext> dbFactory) : IAdminService
{
    private const string ResourceCodeIndex = "IX_Resources_Code";

    public async Task<IReadOnlyList<Resource>> GetResourcesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Resources.AsNoTracking()
            .OrderBy(r => r.Type).ThenBy(r => r.Code)
            .ToListAsync(ct);
    }

    public async Task<Resource> AddResourceAsync(string code, string name, ResourceType type, CancellationToken ct = default)
    {
        var resource = Resource.Create(code, name, type);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await EnsureCodeIsFreeAsync(db, resource.Code, exceptId: null, ct);

        db.Resources.Add(resource);
        await SaveAsync(db, resource.Code, ct);
        return resource;
    }

    public async Task UpdateResourceAsync(int id, string code, string name, bool isActive, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var resource = await db.Resources.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw Gone();
        resource.Update(code, name, isActive);
        await EnsureCodeIsFreeAsync(db, resource.Code, exceptId: id, ct);

        await SaveAsync(db, resource.Code, ct);
    }

    public async Task SetParkingDetailsAsync(int id, ParkingLocation? location, bool hasCharger, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var resource = await db.Resources.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw Gone();
        resource.SetParkingDetails(location, hasCharger);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.People.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task SetPersonActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var person = await db.People.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw Gone();

        person.SetActive(isActive);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureCodeIsFreeAsync(BookingDbContext db, string code, int? exceptId, CancellationToken ct)
    {
        if (await db.Resources.AnyAsync(r => r.Code == code && r.Id != exceptId, ct))
        {
            throw DuplicateCode(code);
        }
    }

    /// <summary>Two admins adding the same code at once both pass the pre-check; the unique index catches the second.</summary>
    private static async Task SaveAsync(BookingDbContext db, string code, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ResourceCodeIndex
        })
        {
            throw DuplicateCode(code);
        }
    }

    private static DomainValidationException DuplicateCode(string code) =>
        new($"A resource with code {code} already exists.");

    private static DomainValidationException Gone() => new("That item no longer exists.");
}
