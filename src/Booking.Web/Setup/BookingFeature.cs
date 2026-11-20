using Booking.Core.Services;
using Booking.Web.Stubs;

namespace Booking.Web.Setup;

/// <summary>Owned by Person A (booking rules).</summary>
public static class BookingFeature
{
    public static IServiceCollection AddBookingFeature(this IServiceCollection services)
    {
        // A4 swaps this for the real BookingService (scoped).
        services.AddSingleton<IBookingService, StubBookingService>();
        return services;
    }
}
