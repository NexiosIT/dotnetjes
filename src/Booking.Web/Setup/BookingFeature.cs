using Booking.Core.Services;
using Booking.Core.Waitlist;
using Booking.Web.Notifications;

namespace Booking.Web.Setup;

/// <summary>Owned by Person A (booking rules), plus the charger waitlist.</summary>
public static class BookingFeature
{
    public static IServiceCollection AddBookingFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IWaitlistService, WaitlistService>();

        services.AddSingleton<ChannelWaitlistNotifier>();
        services.AddSingleton<IWaitlistNotifier>(sp => sp.GetRequiredService<ChannelWaitlistNotifier>());
        services.AddSingleton(new WaitlistEmailSettings(configuration["App:PublicUrl"]));
        services.AddScoped<WaitlistEmailSender>();
        services.AddHostedService<WaitlistNotificationService>();
        return services;
    }
}
