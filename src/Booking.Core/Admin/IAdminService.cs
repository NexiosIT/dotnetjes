using Booking.Core.Entities;

namespace Booking.Core.Admin;

/// <summary>
/// Invalid input throws <see cref="AdminValidationException"/> with a message fit to show the user.
/// People are created from Keycloak on first login (see <c>IPersonDirectory</c>); admins can only activate or deactivate them.
/// </summary>
public interface IAdminService
{
    Task<IReadOnlyList<Resource>> GetResourcesAsync(CancellationToken ct = default);
    Task<Resource> AddResourceAsync(string code, string name, ResourceType type, CancellationToken ct = default);
    Task UpdateResourceAsync(int id, string code, string name, bool isActive, CancellationToken ct = default);
    Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken ct = default);
    Task SetPersonActiveAsync(int id, bool isActive, CancellationToken ct = default);
}
