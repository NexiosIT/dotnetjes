using Booking.Infrastructure.Persistence;
using Booking.Application.People;
using Booking.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.IntegrationTests.People;

[Collection("postgres")]
public sealed class PersonDirectoryTests(PostgresFixture postgres)
{
    private IDbContextFactory<BookingDbContext> _factory = null!;

    private async Task<PersonDirectory> CreateAsync()
    {
        _factory = await postgres.CreateDatabaseAsync();
        return new PersonDirectory(new EfUnitOfWorkFactory(_factory));
    }

    private async Task<int> CountPeopleAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.People.CountAsync();
    }

    [Fact]
    public async Task SignIn_first_time_creates_active_person()
    {
        var directory = await CreateAsync();

        var person = await directory.SignInAsync("sub-1", "Anna Peeters", "anna@org.be");

        Assert.True(person.Id > 0);
        Assert.Equal("Anna Peeters", person.Name);
        Assert.Equal("anna@org.be", person.Email);
        Assert.True(person.IsActive);
        Assert.Equal("sub-1", person.ExternalId);
    }

    [Fact]
    public async Task SignIn_again_returns_same_person_and_refreshes_details()
    {
        var directory = await CreateAsync();

        var first = await directory.SignInAsync("sub-1", "Anna", "anna@old.be");
        var second = await directory.SignInAsync("sub-1", "Anna Peeters", "anna@new.be");

        Assert.Equal(first.Id, second.Id);
        await using var db = await _factory.CreateDbContextAsync();
        var stored = await db.People.SingleAsync();
        Assert.Equal("Anna Peeters", stored.Name);
        Assert.Equal("anna@new.be", stored.Email);
    }

    [Fact]
    public async Task SignIn_same_email_different_sub_creates_separate_person()
    {
        var directory = await CreateAsync();

        var a = await directory.SignInAsync("sub-1", "Anna", "anna@org.be");
        var b = await directory.SignInAsync("sub-2", "Anna 2", "anna@org.be");

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(2, await CountPeopleAsync());
    }

    [Fact]
    public async Task SignIn_keeps_deactivated_person_inactive()
    {
        var directory = await CreateAsync();
        var person = await directory.SignInAsync("sub-1", "Anna", null);
        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.People.Where(p => p.Id == person.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));
        }

        var again = await directory.SignInAsync("sub-1", "Anna", null);

        Assert.False(again.IsActive);
    }

    [Fact]
    public async Task SignIn_concurrent_first_logins_create_one_person()
    {
        var directory = await CreateAsync();

        var people = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => directory.SignInAsync("sub-1", "Anna", null)));

        Assert.Single(people.Select(p => p.Id).Distinct());
        Assert.Equal(1, await CountPeopleAsync());
    }
}
