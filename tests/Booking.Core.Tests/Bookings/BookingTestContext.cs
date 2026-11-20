using Booking.Core.Calendar;
using Booking.Core.Data;
using Booking.Core.Services;
using Booking.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using BookingEntity = Booking.Core.Entities.Booking;

namespace Booking.Core.Tests.Bookings;

/// <summary>
/// A fresh seeded database with the clock frozen at Wed 2026-11-18 09:00 Brussels
/// (today 2026-11-18, last bookable day 2026-12-02), and a real <see cref="BookingService"/> on top.
/// </summary>
public sealed class BookingTestContext
{
    public static readonly DateTimeOffset Now = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private readonly Dictionary<string, int> _people;
    private readonly Dictionary<string, int> _resources;

    private BookingTestContext(
        IDbContextFactory<BookingDbContext> dbFactory,
        FakeTimeProvider time,
        BookingService service,
        Dictionary<string, int> people,
        Dictionary<string, int> resources)
    {
        DbFactory = dbFactory;
        Time = time;
        Service = service;
        _people = people;
        _resources = resources;
    }

    public IDbContextFactory<BookingDbContext> DbFactory { get; }
    public FakeTimeProvider Time { get; }
    public BookingService Service { get; }
    public DateOnly Today { get; } = new(2026, 11, 18);

    public static async Task<BookingTestContext> CreateAsync(PostgresFixture postgres)
    {
        var dbFactory = await postgres.CreateDatabaseAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await SeedData.EnsureSeededAsync(db);

        var people = await db.People.ToDictionaryAsync(p => p.Name, p => p.Id);
        var resources = await db.Resources.ToDictionaryAsync(r => r.Code, r => r.Id);

        var time = new FakeTimeProvider(Now);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");
        var calendar = new OfficeCalendar(time, zone, maxDaysAhead: 14);

        return new BookingTestContext(dbFactory, time, new BookingService(dbFactory, calendar, time), people, resources);
    }

    public int Person(string name) => _people[name];

    public int Resource(string code) => _resources[code];

    public Task DeactivateResource(string code) =>
        Update(db => db.Resources.Where(r => r.Code == code).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false)));

    public Task DeactivatePerson(string name) =>
        Update(db => db.People.Where(p => p.Name == name).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false)));

    /// <summary>Inserts a booking straight into the database, bypassing the service rules.</summary>
    public async Task<int> InsertBooking(string personName, string resourceCode, DateOnly date)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        var resource = await db.Resources.SingleAsync(r => r.Code == resourceCode);
        var booking = new BookingEntity
        {
            PersonId = Person(personName),
            ResourceId = resource.Id,
            ResourceType = resource.Type,
            Date = date,
            CreatedAt = Now.UtcDateTime
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking.Id;
    }

    private async Task Update(Func<BookingDbContext, Task<int>> change)
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        await change(db);
    }
}
