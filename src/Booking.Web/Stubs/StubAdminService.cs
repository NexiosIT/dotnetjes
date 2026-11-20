using Booking.Core.Admin;
using Booking.Core.Entities;

namespace Booking.Web.Stubs;

/// <summary>
/// In-memory stand-in so the admin pages and the user picker can be built before the real AdminService lands.
/// No validation. Deleted in task C1.
/// </summary>
public sealed class StubAdminService : IAdminService
{
    private readonly Lock _lock = new();
    private readonly List<Resource> _resources = StubData.Resources.ToList();
    private readonly List<Person> _people = StubData.People.ToList();

    public Task<IReadOnlyList<Resource>> GetResourcesAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Resource>>(_resources.OrderBy(r => r.Type).ThenBy(r => r.Code).ToList());
        }
    }

    public Task<Resource> AddResourceAsync(string code, string name, ResourceType type, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var resource = new Resource { Id = _resources.Max(r => r.Id) + 1, Code = code, Name = name, Type = type };
            _resources.Add(resource);
            return Task.FromResult(resource);
        }
    }

    public Task UpdateResourceAsync(int id, string code, string name, bool isActive, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var resource = _resources.Single(r => r.Id == id);
            resource.Code = code;
            resource.Name = name;
            resource.IsActive = isActive;
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Person>>(_people.OrderBy(p => p.Name).ToList());
        }
    }

    public Task<Person> AddPersonAsync(string name, string? email, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var person = new Person { Id = _people.Max(p => p.Id) + 1, Name = name, Email = email };
            _people.Add(person);
            return Task.FromResult(person);
        }
    }

    public Task UpdatePersonAsync(int id, string name, string? email, bool isActive, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var person = _people.Single(p => p.Id == id);
            person.Name = name;
            person.Email = email;
            person.IsActive = isActive;
            return Task.CompletedTask;
        }
    }
}
