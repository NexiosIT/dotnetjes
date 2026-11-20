using Booking.Core.Data;
using Booking.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.Core.Tests.Data;

[Collection("postgres")]
public sealed class DatabaseTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_apply_and_seed_runs_once()
    {
        var factory = await postgres.CreateDatabaseAsync();

        await using (var db = await factory.CreateDbContextAsync())
        {
            await SeedData.EnsureSeededAsync(db);
        }
        await using (var db = await factory.CreateDbContextAsync())
        {
            await SeedData.EnsureSeededAsync(db);
        }

        await using var check = await factory.CreateDbContextAsync();
        Assert.Equal(8, await check.Resources.CountAsync(r => r.Type == ResourceType.Desk));
        Assert.Equal(4, await check.Resources.CountAsync(r => r.Type == ResourceType.Parking));
        Assert.Equal(0, await check.People.CountAsync());
    }

    [Fact]
    public async Task Long_codes_names_and_emails_are_stored()
    {
        var factory = await postgres.CreateDatabaseAsync();
        await using var db = await factory.CreateDbContextAsync();

        db.Resources.Add(Resource.Create(new string('X', 30), new string('N', 150), ResourceType.Desk));
        db.People.Add(Person.FromLogin("kc-long", new string('N', 150), new string('e', 250) + "@example.com"));

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Unique_indexes_exist_with_expected_names()
    {
        var factory = await postgres.CreateDatabaseAsync();
        await using var db = await factory.CreateDbContextAsync();

        var indexNames = await db.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'")
            .ToListAsync();

        Assert.Contains("IX_Bookings_Resource_Date", indexNames);
        Assert.Contains("IX_Bookings_Person_Type_Date", indexNames);
        Assert.Contains("IX_Resources_Code", indexNames);
        Assert.Contains("IX_People_ExternalId", indexNames);
    }
}
