namespace Booking.Application.Admin;

/// <summary>A desk or parking spot as the admin pages show it.</summary>
public sealed record ResourceView(int Id, string Code, string Name, ResourceType Type, bool IsActive, ParkingLocation? Location, bool HasCharger)
{
    public static ResourceView From(Resource r) => new(r.Id, r.Code, r.Name, r.Type, r.IsActive, r.Location, r.HasCharger);
}

/// <summary>A person as the admin pages (and the daily summary) see them.</summary>
public sealed record PersonView(int Id, string Name, string? Email, bool IsActive)
{
    public static PersonView From(Person p) => new(p.Id, p.Name, p.Email, p.IsActive);
}

/// <summary>
/// Invalid input throws <see cref="DomainValidationException"/> with a message fit to show the user.
/// People are created from Keycloak on first login (see <c>IPersonDirectory</c>); admins can only activate or deactivate them.
/// Nothing is ever deleted: deactivate instead.
/// </summary>
public interface IAdminService
{
    Task<IReadOnlyList<ResourceView>> GetResourcesAsync(CancellationToken ct = default);
    Task<ResourceView> AddResourceAsync(string code, string name, ResourceType type, CancellationToken ct = default);
    Task UpdateResourceAsync(int id, string code, string name, bool isActive, CancellationToken ct = default);
    /// <summary>Where a parking spot is and whether it has a charger. Desks can't have either.</summary>
    Task SetParkingDetailsAsync(int id, ParkingLocation? location, bool hasCharger, CancellationToken ct = default);
    Task<IReadOnlyList<PersonView>> GetPeopleAsync(CancellationToken ct = default);
    Task SetPersonActiveAsync(int id, bool isActive, CancellationToken ct = default);
}
