using Booking.Core.Services;

namespace Booking.Web.Setup;

/// <summary>Owned by Person A (booking rules).</summary>
public static class BookingFeature
{
    public static IServiceCollection AddBookingFeature(this IServiceCollection services)
    {
        services.AddScoped<IBookingService, BookingService>();
        return services;
    }
}
