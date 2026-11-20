using Microsoft.EntityFrameworkCore;

namespace Booking.Core.Data;

public static class SeedData
{
    /// <summary>
    /// Adds sample desks and parking spots, only when the database has no resources yet.
    /// People aren't seeded: they're created from Keycloak on their first login.
    /// </summary>
    public static async Task EnsureSeededAsync(BookingDbContext db, CancellationToken ct = default)
    {
        if (await db.Resources.AnyAsync(ct))
        {
            return;
        }

        for (var i = 1; i <= 8; i++)
        {
            db.Resources.Add(new Resource { Code = $"D{i}", Name = $"Desk {i}", Type = ResourceType.Desk });
        }
        (ParkingLocation Location, bool HasCharger)[] parking =
        [
            (ParkingLocation.Basement, true),
            (ParkingLocation.Basement, false),
            (ParkingLocation.Outside, true),
            (ParkingLocation.BehindTheBuilding, false)
        ];
        for (var i = 1; i <= parking.Length; i++)
        {
            var (location, hasCharger) = parking[i - 1];
            db.Resources.Add(new Resource
            {
                Code = $"P{i}", Name = $"Parking {i}", Type = ResourceType.Parking,
                Location = location, HasCharger = hasCharger
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
