using Booking.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Booking.Core.Data;

public static class SeedData
{
    private static readonly string[] PeopleNames = ["Anna", "Ben", "Chris", "Dana", "Eli", "Fien"];

    /// <summary>Adds sample desks, parking spots and people, only when the database has no people yet.</summary>
    public static async Task EnsureSeededAsync(BookingDbContext db, CancellationToken ct = default)
    {
        if (await db.People.AnyAsync(ct))
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
        foreach (var name in PeopleNames)
        {
            db.People.Add(new Person { Name = name, Email = $"{name.ToLowerInvariant()}@example.com" });
        }

        await db.SaveChangesAsync(ct);
    }
}
