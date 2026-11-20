using Booking.Application.Admin;
using Booking.Application.Bookings;
using Booking.Application.Notifications;
using Booking.Application.People;
using Booking.Application.Waitlist;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.Application;

public static class DependencyInjection
{
    /// <summary>The use cases. Their ports (persistence, email, notifier, calendar) come from the infrastructure.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IPersonDirectory, PersonDirectory>();
        services.AddScoped<DailySummarySender>();
        services.AddScoped<WaitlistEmailSender>();
        return services;
    }
}
