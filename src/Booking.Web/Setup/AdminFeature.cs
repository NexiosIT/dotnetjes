using Booking.Core.Admin;
using Booking.Web.Stubs;

namespace Booking.Web.Setup;

/// <summary>Owned by Person C (admin and daily email).</summary>
public static class AdminFeature
{
    public static IServiceCollection AddAdminFeature(this IServiceCollection services, IConfiguration configuration)
    {
        // C1 swaps this for the real AdminService (scoped); C5 adds email and the background job.
        services.AddSingleton<IAdminService, StubAdminService>();
        return services;
    }
}
