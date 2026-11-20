using Booking.Web.CurrentUser;

namespace Booking.Web.Setup;

/// <summary>Owned by Person B (day view).</summary>
public static class DayViewFeature
{
    public static IServiceCollection AddDayViewFeature(this IServiceCollection services)
    {
        // B1 swaps this for LocalStorageCurrentUser.
        services.AddScoped<ICurrentUser, InMemoryCurrentUser>();
        return services;
    }
}
