using Booking.Core.Data;

namespace Booking.Core.Tests.Infrastructure;

/// <summary>The six people the app used to seed. Tests refer to them by name ("Anna", "Dana", …).</summary>
internal static class DemoPeople
{
    private static readonly string[] Names = ["Anna", "Ben", "Chris", "Dana", "Eli", "Fien"];

    public static async Task AddAsync(BookingDbContext db)
    {
        foreach (var name in Names)
        {
            db.People.Add(new Person { Name = name, Email = $"{name.ToLowerInvariant()}@example.com" });
        }
        await db.SaveChangesAsync();
    }
}
