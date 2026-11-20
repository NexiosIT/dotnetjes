using Booking.Core.Services;

namespace Booking.Web.Setup;

/// <summary>Owned by Person A (booking rules).</summary>
public static class BookingFeature
{
    public static IServiceCollection AddBookingFeature(this IServiceCollection services)
    {
        // A4 swaps this for the real BookingService (scoped).
        // Fully qualified (no using) so deleting the Stubs folder never breaks another person's Setup file.
        services.AddSingleton<IBookingService, Stubs.StubBookingService>();
        return services;
    }
}
