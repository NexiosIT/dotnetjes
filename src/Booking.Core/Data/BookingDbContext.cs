using Booking.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Booking.Core.Data;

public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public const string ResourceDateIndex = "IX_Bookings_Resource_Date";
    public const string PersonTypeDateIndex = "IX_Bookings_Person_Type_Date";
    public const string ResourceCodeIndex = "IX_Resources_Code";
    public const string PersonExternalIdIndex = "IX_People_ExternalId";

    public DbSet<Person> People => Set<Person>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Entities.Booking> Bookings => Set<Entities.Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Text columns have no length limit on purpose: a too-long value would fail as a
        // DbUpdateException, which admin pages don't catch (they only catch AdminValidationException).
        modelBuilder.Entity<Resource>(resource =>
        {
            resource.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
            resource.HasIndex(r => r.Code).IsUnique().HasDatabaseName(ResourceCodeIndex);
        });

        // Unique, but Postgres allows many NULLs, so people who never logged in don't clash.
        modelBuilder.Entity<Person>()
            .HasIndex(p => p.ExternalId).IsUnique().HasDatabaseName(PersonExternalIdIndex);

        modelBuilder.Entity<Entities.Booking>(booking =>
        {
            booking.Property(b => b.ResourceType).HasConversion<string>().HasMaxLength(20);
            booking.HasIndex(b => new { b.ResourceId, b.Date }).IsUnique().HasDatabaseName(ResourceDateIndex);
            booking.HasIndex(b => new { b.PersonId, b.ResourceType, b.Date }).IsUnique().HasDatabaseName(PersonTypeDateIndex);
        });
    }
}
