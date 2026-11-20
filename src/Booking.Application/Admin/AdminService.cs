namespace Booking.Application.Admin;

/// <summary>Resource and people management. Validation lives on <see cref="Resource"/>; code uniqueness is checked here.</summary>
public sealed class AdminService(IUnitOfWorkFactory unitsOfWork) : IAdminService
{
    public async Task<IReadOnlyList<ResourceView>> GetResourcesAsync(CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);
        return (await uow.Resources.ListAsync(ct)).Select(ResourceView.From).ToList();
    }

    public async Task<ResourceView> AddResourceAsync(string code, string name, ResourceType type, CancellationToken ct = default)
    {
        var resource = Resource.Create(code, name, type);

        await using var uow = await unitsOfWork.CreateAsync(ct);
        await EnsureCodeIsFreeAsync(uow, resource.Code, exceptId: null, ct);
        uow.Resources.Add(resource);
        await SaveAsync(uow, resource.Code, ct);
        return ResourceView.From(resource);
    }

    public async Task UpdateResourceAsync(int id, string code, string name, bool isActive, CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);
        var resource = await uow.Resources.GetAsync(id, ct) ?? throw Gone();
        resource.Update(code, name, isActive);
        await EnsureCodeIsFreeAsync(uow, resource.Code, exceptId: id, ct);
        await SaveAsync(uow, resource.Code, ct);
    }

    public async Task SetParkingDetailsAsync(int id, ParkingLocation? location, bool hasCharger, CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);
        var resource = await uow.Resources.GetAsync(id, ct) ?? throw Gone();
        resource.SetParkingDetails(location, hasCharger);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PersonView>> GetPeopleAsync(CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);
        return (await uow.People.ListAsync(ct)).Select(PersonView.From).ToList();
    }

    public async Task SetPersonActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);
        var person = await uow.People.GetAsync(id, ct) ?? throw Gone();
        person.SetActive(isActive);
        await uow.SaveChangesAsync(ct);
    }

    private static async Task EnsureCodeIsFreeAsync(IUnitOfWork uow, string code, int? exceptId, CancellationToken ct)
    {
        if (await uow.Resources.CodeExistsAsync(code, exceptId, ct))
        {
            throw DuplicateCode(code);
        }
    }

    /// <summary>Two admins adding the same code at once both pass the pre-check; the unique index catches the second.</summary>
    private static async Task SaveAsync(IUnitOfWork uow, string code, CancellationToken ct)
    {
        if (await uow.SaveChangesAsync(ct) is SaveConflict.ResourceCodeTaken)
        {
            throw DuplicateCode(code);
        }
    }

    private static DomainValidationException DuplicateCode(string code) => new($"A resource with code {code} already exists.");

    private static DomainValidationException Gone() => new("That item no longer exists.");
}
