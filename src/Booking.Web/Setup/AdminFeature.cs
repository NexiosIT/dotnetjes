using Booking.Core.Admin;

namespace Booking.Web.Setup;

/// <summary>Owned by Person C (admin and daily email).</summary>
public static class AdminFeature
{
    public static IServiceCollection AddAdminFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAdminService, AdminService>();
        return services;
    }
}
