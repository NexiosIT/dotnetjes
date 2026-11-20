using Booking.Core.Calendar;
using Booking.Core.Data;
using Booking.Web.Components;
using Booking.Web.Setup;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Booking")));
builder.Services.AddSingleton(TimeProvider.System);

if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("dotnetjes-booking");
}
builder.Services.AddSingleton<IOfficeCalendar>(sp => new OfficeCalendar(
    sp.GetRequiredService<TimeProvider>(),
    TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["App:TimeZone"] ?? "Europe/Brussels"),
    builder.Configuration.GetValue("Booking:MaxDaysAhead", 14)));

builder.Services.AddBookingFeature(builder.Configuration);
builder.Services.AddDayViewFeature();
builder.Services.AddAdminFeature(builder.Configuration);
builder.Services.AddAuthFeature(builder.Configuration);

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
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapAuthEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
