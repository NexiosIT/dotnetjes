using Microsoft.EntityFrameworkCore;

namespace Booking.Core.Data;

public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public const string ResourceDateIndex = "IX_Bookings_Resource_Date";
    public const string PersonTypeDateIndex = "IX_Bookings_Person_Type_Date";
    public const string ResourceCodeIndex = "IX_Resources_Code";
    public const string PersonExternalIdIndex = "IX_People_ExternalId";
    public const string WaitlistIndex = "IX_Waitlist_Person_Date";

    public DbSet<Person> People => Set<Person>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Domain.Bookings.Booking> Bookings => Set<Domain.Bookings.Booking>();
    public DbSet<WaitlistEntry> Waitlist => Set<WaitlistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Resource>(resource =>
        {
            resource.Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
            resource.Property(r => r.Location).HasConversion<string>();
            resource.HasIndex(r => r.Code).IsUnique().HasDatabaseName(ResourceCodeIndex);
        });

        modelBuilder.Entity<Person>()
            .HasIndex(p => p.ExternalId).IsUnique().HasDatabaseName(PersonExternalIdIndex);

        modelBuilder.Entity<Domain.Bookings.Booking>(booking =>
        {
            booking.Property(b => b.ResourceType).HasConversion<string>().HasMaxLength(20);
            booking.HasIndex(b => new { b.ResourceId, b.Date }).IsUnique().HasDatabaseName(ResourceDateIndex);
            booking.HasIndex(b => new { b.PersonId, b.ResourceType, b.Date }).IsUnique().HasDatabaseName(PersonTypeDateIndex);
        });

        modelBuilder.Entity<WaitlistEntry>(entry =>
        {
            entry.ToTable("Waitlist");
            entry.HasIndex(w => new { w.PersonId, w.Date }).IsUnique().HasDatabaseName(WaitlistIndex);
        });
    }
}
