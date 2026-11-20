using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Booking.Core.Data;

/// <summary>Used only by <c>dotnet ef</c> when creating migrations.</summary>
public class BookingDbContextDesignFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=booking;Username=booking;Password=booking")
            .Options);
}
