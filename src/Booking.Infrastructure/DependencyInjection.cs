using Booking.Application.Waitlist;
using Booking.Infrastructure.BackgroundJobs;
using Booking.Infrastructure.Email;
using Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Everything the use cases need from the outside world: PostgreSQL (<c>ConnectionStrings:Booking</c>), the clock and
    /// office calendar (<c>App:TimeZone</c>, <c>Booking:MaxDaysAhead</c>), SMTP (<c>Smtp</c>), and the background jobs:
    /// the daily summary (<c>Summary:SendAt</c>) and the waitlist emails (<c>App:PublicUrl</c>).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextFactory<BookingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Booking")));
        services.AddSingleton<IUnitOfWorkFactory, EfUnitOfWorkFactory>();
        services.AddSingleton<IDayOverviewQuery, DayOverviewQuery>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOfficeCalendar>(sp => new OfficeCalendar(
            sp.GetRequiredService<TimeProvider>(),
            TimeZoneInfo.FindSystemTimeZoneById(configuration["App:TimeZone"] ?? "Europe/Brussels"),
            configuration.GetValue("Booking:MaxDaysAhead", 14)));

        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<DailySummaryService>();

        services.AddSingleton<ChannelWaitlistNotifier>();
        services.AddSingleton<IWaitlistNotifier>(sp => sp.GetRequiredService<ChannelWaitlistNotifier>());
        services.AddSingleton(new WaitlistEmailSettings(configuration["App:PublicUrl"]));
        services.AddHostedService<WaitlistNotificationService>();
        return services;
    }

    /// <summary>Applies pending migrations and adds the sample desks and parking spots to an empty database.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var db = await services.GetRequiredService<IDbContextFactory<BookingDbContext>>().CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
        await SeedData.EnsureSeededAsync(db, ct);
    }
}
