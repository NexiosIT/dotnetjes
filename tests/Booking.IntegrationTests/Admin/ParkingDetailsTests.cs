using Booking.Application.Admin;
using Booking.Infrastructure.Persistence;
using Booking.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Booking.IntegrationTests.Admin;

[Collection("postgres")]
public sealed class ParkingDetailsTests(PostgresFixture postgres)
{
    private async Task<(AdminService Service, IDbContextFactory<BookingDbContext> Db)> CreateAsync()
    {
        var factory = await postgres.CreateDatabaseAsync();
        await using (var db = await factory.CreateDbContextAsync())
        {
            await SeedData.EnsureSeededAsync(db);
        }
        return (new AdminService(new EfUnitOfWorkFactory(factory)), factory);
    }

    private static async Task<ResourceView> Get(AdminService service, string code) =>
        (await service.GetResourcesAsync()).Single(r => r.Code == code);

    [Fact]
    public async Task GivenASeededDatabase_WhenReadingResources_ThenParkingSpotsHaveLocationAndChargerAndDesksHaveNone()
    {
        var (service, _) = await CreateAsync();

        Assert.Equal((ParkingLocation.Basement, true), Details(await Get(service, "P1")));
        Assert.Equal((ParkingLocation.Basement, false), Details(await Get(service, "P2")));
        Assert.Equal((ParkingLocation.Outside, true), Details(await Get(service, "P3")));
        Assert.Equal((ParkingLocation.BehindTheBuilding, false), Details(await Get(service, "P4")));
        Assert.All((await service.GetResourcesAsync()).Where(r => r.Type == ResourceType.Desk),
            d => Assert.Equal((null, false), Details(d)));
    }

    [Fact]
    public async Task GivenParkingSpotP2_WhenSettingLocationAndCharger_ThenBothAreStored()
    {
        var (service, _) = await CreateAsync();
        var p2 = await Get(service, "P2");

        await service.SetParkingDetailsAsync(p2.Id, ParkingLocation.Outside, hasCharger: true);

        Assert.Equal((ParkingLocation.Outside, true), Details(await Get(service, "P2")));
    }

    [Fact]
    public async Task GivenParkingSpotP1_WhenSettingNoLocationAndNoCharger_ThenBothAreCleared()
    {
        var (service, _) = await CreateAsync();
        var p1 = await Get(service, "P1");

        await service.SetParkingDetailsAsync(p1.Id, null, hasCharger: false);

        Assert.Equal((null, false), Details(await Get(service, "P1")));
    }

    [Fact]
    public async Task GivenAnInactiveRenamedSpot_WhenSettingParkingDetails_ThenNameAndInactiveStateAreKept()
    {
        var (service, _) = await CreateAsync();
        var p4 = await Get(service, "P4");
        await service.UpdateResourceAsync(p4.Id, "P4", "Parking 4", isActive: false);

        await service.SetParkingDetailsAsync(p4.Id, ParkingLocation.Basement, hasCharger: true);

        var stored = await Get(service, "P4");
        Assert.Equal("Parking 4", stored.Name);
        Assert.False(stored.IsActive);
    }

    [Theory]
    [InlineData(ParkingLocation.Outside, false)]
    [InlineData(null, true)]
    public async Task GivenADesk_WhenSettingParkingDetails_ThenItIsRejectedBecauseOnlyParkingHasThem(ParkingLocation? location, bool hasCharger)
    {
        var (service, _) = await CreateAsync();
        var d1 = await Get(service, "D1");

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.SetParkingDetailsAsync(d1.Id, location, hasCharger));

        Assert.Equal("Only parking spots have a location or charger.", ex.Message);
    }

    [Fact]
    public async Task GivenAnUnknownId_WhenSettingParkingDetails_ThenItFailsWithNoLongerExists()
    {
        var (service, _) = await CreateAsync();

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.SetParkingDetailsAsync(9999, ParkingLocation.Outside, false));

        Assert.Equal("That item no longer exists.", ex.Message);
    }

    [Fact]
    public void WhenListingParkingLocations_ThenEachHasALabelAndAllLocationsAreListed()
    {
        foreach (var location in ParkingLocations.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(ParkingLocations.Label(location)));
        }
        Assert.Equal("Behind the building", ParkingLocations.Label(ParkingLocation.BehindTheBuilding));
        Assert.Equal(Enum.GetValues<ParkingLocation>().Length, ParkingLocations.All.Count);
    }

    private static (ParkingLocation?, bool) Details(ResourceView r) => (r.Location, r.HasCharger);
}
