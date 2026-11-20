using Booking.Core.Entities;
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
        for (var i = 1; i <= 4; i++)
        {
            db.Resources.Add(new Resource { Code = $"P{i}", Name = $"Parking {i}", Type = ResourceType.Parking });
        }

        await db.SaveChangesAsync(ct);
    }
}
