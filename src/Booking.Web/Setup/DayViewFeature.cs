using Booking.Web.CurrentUser;

namespace Booking.Web.Setup;

/// <summary>Owned by Person B (day view).</summary>
public static class DayViewFeature
{
    public static IServiceCollection AddDayViewFeature(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUser, LocalStorageCurrentUser>();
        return services;
    }
}
