using Booking.Core.Admin;
using Booking.Core.Data;
using Booking.Core.Entities;
using Booking.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.Core.Tests.Admin;

[Collection("postgres")]
public sealed class AdminServiceTests(PostgresFixture postgres)
{
    private async Task<AdminService> CreateAsync()
    {
        var factory = await postgres.CreateDatabaseAsync();
        await using (var db = await factory.CreateDbContextAsync())
        {
            await SeedData.EnsureSeededAsync(db);
        }
        return new AdminService(factory);
    }

    [Fact]
    public async Task AddResource_normalizes_code()
    {
        var service = await CreateAsync();

        var added = await service.AddResourceAsync(" d9 ", " Desk 9 ", ResourceType.Desk);

        Assert.Equal("D9", added.Code);
        Assert.Equal("Desk 9", added.Name);
        var stored = (await service.GetResourcesAsync()).Single(r => r.Id == added.Id);
        Assert.Equal("D9", stored.Code);
        Assert.Equal("Desk 9", stored.Name);
    }

    [Fact]
    public async Task AddResource_rejects_duplicate_code()
    {
        var service = await CreateAsync();

        var ex = await Assert.ThrowsAsync<AdminValidationException>(
            () => service.AddResourceAsync(" d1 ", "x", ResourceType.Desk));

        Assert.Equal("A resource with code D1 already exists.", ex.Message);
    }

    [Fact]
    public async Task UpdateResource_rejects_code_of_another_resource()
    {
        var service = await CreateAsync();
        var d2 = (await service.GetResourcesAsync()).Single(r => r.Code == "D2");

        var ex = await Assert.ThrowsAsync<AdminValidationException>(
            () => service.UpdateResourceAsync(d2.Id, "d1", "Desk 2", true));

        Assert.Equal("A resource with code D1 already exists.", ex.Message);
    }

    [Fact]
    public async Task AddResource_requires_code_and_name()
    {
        var service = await CreateAsync();

        var noCode = await Assert.ThrowsAsync<AdminValidationException>(
            () => service.AddResourceAsync("", "Desk 9", ResourceType.Desk));
        var noName = await Assert.ThrowsAsync<AdminValidationException>(
            () => service.AddResourceAsync("D9", "", ResourceType.Desk));

        Assert.Equal("Code is required.", noCode.Message);
        Assert.Equal("Name is required.", noName.Message);
    }

    [Fact]
    public async Task UpdateResource_toggles_active()
    {
        var service = await CreateAsync();
        var p4 = (await service.GetResourcesAsync()).Single(r => r.Code == "P4");

        await service.UpdateResourceAsync(p4.Id, "P4", "Parking 4", false);

        var stored = (await service.GetResourcesAsync()).Single(r => r.Id == p4.Id);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task Update_unknown_id_fails()
    {
        var service = await CreateAsync();

        var resource = await Assert.ThrowsAsync<AdminValidationException>(
            () => service.UpdateResourceAsync(9999, "X1", "X", true));
        var person = await Assert.ThrowsAsync<AdminValidationException>(
            () => service.UpdatePersonAsync(9999, "X", null, true));

        Assert.Equal("That item no longer exists.", resource.Message);
        Assert.Equal("That item no longer exists.", person.Message);
    }

    [Fact]
    public async Task AddPerson_validates_email()
    {
        var service = await CreateAsync();

        var ex = await Assert.ThrowsAsync<AdminValidationException>(
            () => service.AddPersonAsync("Gert", "gert"));
        var added = await service.AddPersonAsync("Gert", "");

        Assert.Equal("Email must contain '@'.", ex.Message);
        var stored = (await service.GetPeopleAsync()).Single(p => p.Id == added.Id);
        Assert.Null(stored.Email);
    }

    [Fact]
    public async Task UpdatePerson_accepts_null_email()
    {
        var service = await CreateAsync();
        var dana = (await service.GetPeopleAsync()).Single(p => p.Name == "Dana");

        await service.UpdatePersonAsync(dana.Id, "Dana", null, true);

        var stored = (await service.GetPeopleAsync()).Single(p => p.Id == dana.Id);
        Assert.Null(stored.Email);
    }

    [Fact]
    public async Task GetPeople_ordered_by_name()
    {
        var service = await CreateAsync();

        var people = await service.GetPeopleAsync();

        Assert.Equal("Anna", people[0].Name);
        Assert.Equal("Fien", people[^1].Name);
    }
}
