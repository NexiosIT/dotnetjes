using Booking.Core.Admin;
using Booking.Core.Notifications;
using Booking.Web.Notifications;

namespace Booking.Web.Setup;

/// <summary>Owned by Person C (admin and daily email).</summary>
public static class AdminFeature
{
    public static IServiceCollection AddAdminFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAdminService, AdminService>();

        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<DailySummarySender>();
        services.AddHostedService<DailySummaryService>();
        return services;
    }
}
