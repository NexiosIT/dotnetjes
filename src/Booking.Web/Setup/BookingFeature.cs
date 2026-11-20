using Booking.Core.Services;
using Booking.Core.Waitlist;

namespace Booking.Web.Setup;

/// <summary>Owned by Person A (booking rules).</summary>
public static class BookingFeature
{
    public static IServiceCollection AddBookingFeature(this IServiceCollection services)
    {
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddSingleton<IWaitlistNotifier, NoWaitlistNotifier>();
        return services;
    }
}
