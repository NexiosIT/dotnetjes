using Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Booking.IntegrationTests.Infrastructure;

/// <summary>
/// One postgres:18-alpine container (same image as docker-compose.yml) for the whole test run. Each test gets its own freshly migrated database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Every test database has its own connection pool, whose idle connections stay open for a while:
    // past ~100 tests the default max_connections (100) runs out ("53300: too many clients").
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithCommand("-c", "max_connections=1000")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<IDbContextFactory<BookingDbContext>> CreateDatabaseAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = $"test_{Guid.NewGuid():N}"
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var db = new BookingDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        return new PooledDbContextFactory<BookingDbContext>(options);
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
