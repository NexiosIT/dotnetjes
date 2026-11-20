using Booking.Core.Data;
using Booking.Core.Entities;
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
        Assert.Equal(6, await check.People.CountAsync());
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
    }
}
