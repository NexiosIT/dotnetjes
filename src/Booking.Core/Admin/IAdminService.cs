using Booking.Core.Entities;

namespace Booking.Core.Admin;

/// <summary>
/// Invalid input throws <see cref="AdminValidationException"/> with a message fit to show the user.
/// Email is optional: null or blank is stored as null.
/// </summary>
public interface IAdminService
{
    Task<IReadOnlyList<Resource>> GetResourcesAsync(CancellationToken ct = default);
    Task<Resource> AddResourceAsync(string code, string name, ResourceType type, CancellationToken ct = default);
    Task UpdateResourceAsync(int id, string code, string name, bool isActive, CancellationToken ct = default);
    /// <summary>Where a parking spot is and whether it has a charger. Desks can't have either.</summary>
    Task SetParkingDetailsAsync(int id, ParkingLocation? location, bool hasCharger, CancellationToken ct = default);
    Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken ct = default);
    Task<Person> AddPersonAsync(string name, string? email, CancellationToken ct = default);
    Task UpdatePersonAsync(int id, string name, string? email, bool isActive, CancellationToken ct = default);
}
