using Booking.Core.Data;
using Booking.Core.Entities;
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
        var normalizedCode = NormalizeCode(code);
        var trimmedName = Required(name, "Name");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await EnsureCodeIsFreeAsync(db, normalizedCode, exceptId: null, ct);

        var resource = new Resource { Code = normalizedCode, Name = trimmedName, Type = type };
        db.Resources.Add(resource);
        await SaveAsync(db, normalizedCode, ct);
        return resource;
    }

    public async Task UpdateResourceAsync(int id, string code, string name, bool isActive, CancellationToken ct = default)
    {
        var normalizedCode = NormalizeCode(code);
        var trimmedName = Required(name, "Name");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var resource = await db.Resources.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw Gone();
        await EnsureCodeIsFreeAsync(db, normalizedCode, exceptId: id, ct);

        resource.Code = normalizedCode;
        resource.Name = trimmedName;
        resource.IsActive = isActive;
        await SaveAsync(db, normalizedCode, ct);
    }

    public async Task SetParkingDetailsAsync(int id, ParkingLocation? location, bool hasCharger, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var resource = await db.Resources.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw Gone();
        if (resource.Type != ResourceType.Parking && (location is not null || hasCharger))
        {
            throw new AdminValidationException("Only parking spots have a location or charger.");
        }

        resource.Location = location;
        resource.HasCharger = hasCharger;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.People.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Person> AddPersonAsync(string name, string? email, CancellationToken ct = default)
    {
        var person = new Person { Name = Required(name, "Name"), Email = NormalizeEmail(email) };

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.People.Add(person);
        await db.SaveChangesAsync(ct);
        return person;
    }

    public async Task UpdatePersonAsync(int id, string name, string? email, bool isActive, CancellationToken ct = default)
    {
        var trimmedName = Required(name, "Name");
        var normalizedEmail = NormalizeEmail(email);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var person = await db.People.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw Gone();

        person.Name = trimmedName;
        person.Email = normalizedEmail;
        person.IsActive = isActive;
        await db.SaveChangesAsync(ct);
    }

    private static string NormalizeCode(string? code) => Required(code, "Code").ToUpperInvariant();

    private static string Required(string? value, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new AdminValidationException($"{field} is required.");
        }
        return trimmed;
    }

    private static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (!trimmed.Contains('@'))
        {
            throw new AdminValidationException("Email must contain '@'.");
        }
        return trimmed;
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

    private static AdminValidationException DuplicateCode(string code) =>
        new($"A resource with code {code} already exists.");

    private static AdminValidationException Gone() => new("That item no longer exists.");
}
