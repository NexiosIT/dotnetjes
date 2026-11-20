using Booking.Core.Admin;

namespace Booking.Web.Setup;

/// <summary>Owned by Person C (admin and daily email).</summary>
public static class AdminFeature
{
    public static IServiceCollection AddAdminFeature(this IServiceCollection services, IConfiguration configuration)
    {
        // C1 swaps this for the real AdminService (scoped); C5 adds email and the background job.
        // Fully qualified (no using) so deleting the Stubs folder never breaks another person's Setup file.
        services.AddSingleton<IAdminService, Stubs.StubAdminService>();
        return services;
    }
}
