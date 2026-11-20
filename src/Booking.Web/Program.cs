using Booking.Core.Calendar;
using Booking.Core.Data;
using Booking.Web.Components;
using Booking.Web.Setup;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Shared infrastructure. Don't change this file after the skeleton:
// register your own services in Setup/*Feature.cs instead.
builder.Services.AddDbContextFactory<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Booking")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IOfficeCalendar>(sp => new OfficeCalendar(
    sp.GetRequiredService<TimeProvider>(),
    TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["App:TimeZone"] ?? "Europe/Brussels"),
    builder.Configuration.GetValue("Booking:MaxDaysAhead", 14)));

builder.Services.AddBookingFeature();                    // Person A
builder.Services.AddDayViewFeature();                    // Person B
builder.Services.AddAdminFeature(builder.Configuration); // Person C

var app = builder.Build();

var dbFactory = app.Services.GetRequiredService<IDbContextFactory<BookingDbContext>>();
await using (var db = await dbFactory.CreateDbContextAsync())
{
    await db.Database.MigrateAsync();
    await SeedData.EnsureSeededAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
