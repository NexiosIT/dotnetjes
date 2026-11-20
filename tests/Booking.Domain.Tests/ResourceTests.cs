namespace Booking.Domain.Tests;

public class ResourceTests
{
    [Fact]
    public void Create_trims_and_uppercases_the_code()
    {
        var desk = Resource.Create(" d9 ", " Desk 9 ", ResourceType.Desk);

        Assert.Equal("D9", desk.Code);
        Assert.Equal("Desk 9", desk.Name);
        Assert.Equal(ResourceType.Desk, desk.Type);
        Assert.True(desk.IsActive);
        Assert.Null(desk.Location);
        Assert.False(desk.HasCharger);
    }

    [Theory]
    [InlineData(" ", "Desk", "Code is required.")]
    [InlineData("D1", "", "Name is required.")]
    public void Create_requires_code_and_name(string code, string name, string message) =>
        Assert.Equal(message, Assert.Throws<DomainValidationException>(() => Resource.Create(code, name, ResourceType.Desk)).Message);

    [Fact]
    public void Update_changes_code_name_and_active()
    {
        var desk = Fixtures.Desk();

        desk.Update(" d2 ", "Window desk", isActive: false);

        Assert.Equal("D2", desk.Code);
        Assert.Equal("Window desk", desk.Name);
        Assert.False(desk.IsActive);
    }

    [Fact]
    public void Update_requires_code_and_name() =>
        Assert.Throws<DomainValidationException>(() => Fixtures.Desk().Update("", "x", true));

    [Fact]
    public void Parking_spot_gets_location_and_charger()
    {
        var spot = Resource.Create("P9", "Parking 9", ResourceType.Parking);

        spot.SetParkingDetails(ParkingLocation.Outside, hasCharger: true);

        Assert.Equal(ParkingLocation.Outside, spot.Location);
        Assert.True(spot.HasCharger);
    }

    [Theory]
    [InlineData(ParkingLocation.Outside, false)]
    [InlineData(null, true)]
    public void Desk_cannot_get_location_or_charger(ParkingLocation? location, bool charger) =>
        Assert.Equal("Only parking spots have a location or charger.",
            Assert.Throws<DomainValidationException>(() => Fixtures.Desk().SetParkingDetails(location, charger)).Message);

    [Fact]
    public void Desk_accepts_no_parking_details() => Fixtures.Desk().SetParkingDetails(null, false);

    [Fact]
    public void Charger_spot_is_an_active_parking_spot_with_charger()
    {
        var spot = Fixtures.ChargerSpot();
        Assert.True(spot.IsChargerSpot);

        spot.Update(spot.Code, spot.Name, isActive: false);
        Assert.False(spot.IsChargerSpot);
        Assert.False(Fixtures.Desk().IsChargerSpot);
        Assert.False(Resource.Create("P2", "Parking 2", ResourceType.Parking).IsChargerSpot);
    }
}
