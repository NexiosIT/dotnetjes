# CLAUDE.md

Desk & parking booking app: .NET 10, Blazor Web App (Interactive Server, `prerender: false`), EF Core 10 + Npgsql/PostgreSQL, Keycloak (OIDC) login, Tailwind CSS v4, xUnit v2. See README.md for running and configuration.

## Commands

```bash
dotnet build                      # also compiles Tailwind (Styles/app.css → wwwroot/app.css)
dotnet test                       # Core tests need Docker running (Testcontainers Postgres)
dotnet test tests/Booking.Web.Tests              # fast, no Docker
dotnet test tests/Booking.Core.Tests --filter "FullyQualifiedName~SwitchTests"
dotnet ef migrations add <Name> --project src/Booking.Core --startup-project src/Booking.Web --output-dir Data/Migrations
docker compose up --build         # app :8080, Mailpit UI :8025
```

`dotnet run` needs the Keycloak settings in user-secrets (project `src/Booking.Web`); without them the app refuses to start.

## Architecture

- **`Booking.Core`** holds every rule: booking, switching, cancelling, the charger waitlist, who gets which email. It has no web dependencies. The UI never decides a rule; it asks Core and shows the result.
- **`Booking.Web`** is the Blazor UI plus infrastructure: Keycloak login (`Setup/AuthFeature.cs`), SMTP (`Notifications/SmtpEmailSender.cs`), background services, DI. Each feature registers its services in `Setup/*Feature.cs`, not in `Program.cs`.
- Data access goes only through `IDbContextFactory<BookingDbContext>`, one context per service method.
- **Races are decided by unique indexes** (`IX_Bookings_Resource_Date`, `IX_Bookings_Person_Type_Date`, `IX_Waitlist_Person_Date`). Pre-checks give the normal case a clean error; the index catch (`ConflictFrom`) handles simultaneous clicks. Keep both when changing booking code.
- **"Today" and bookable days** come from `IOfficeCalendar` (office time zone, weekdays, `Booking:MaxDaysAhead`). Never use `DateTime.Now`; inject `TimeProvider`.
- **The current user** comes from `ICurrentUser` (resolved from the Keycloak `sub` claim). Admin pages are everything under `Components/Pages/Admin/`, locked by `_Imports.razor` with the `"Admin"` policy (Keycloak client role `Admin`).
- **Emails** that react to a user action go through `IWaitlistNotifier` → in-memory channel → `BackgroundService`, so a click never waits on SMTP. One failing address is logged and never stops the others.
- Results are values, not exceptions: `BookingResult` / `BookingError`, `WaitlistResult` / `WaitlistError`. User-facing text for each error lives in `BookingMessages` / `WaitlistMessages`; keep it plain language.

## Conventions

- **TDD.** Write the failing test first. Core behaviour is tested against real Postgres via `BookingTestContext` (frozen clock: today is Wed 2026-11-18; seeded desks D1–D8, parking P1–P4 with chargers on P1 and P3; demo people Anna, Ben, Chris, Dana, Eli, Fien). Logic that would sit in a `.razor` file goes in a small static class next to it (`SlotFilter`, `ChargerWaitlist`) so it can be unit-tested.
- **Tailwind:** write full class names in markup or C# string constants. Never build them from pieces (`$"bg-{accent}-500"`): the Tailwind scanner wouldn't find them. Rebuild after adding classes.
- **Secrets** never go in appsettings or git: user-secrets for `dotnet run`, `.env` (gitignored) for Docker.
- **No comments inside method bodies.** Put the why in the XML doc comment (`///`) on the member, or above a field/class. Before every commit, run the `remove-method-comments` skill on the changed files: a `PreToolUse` hook (`.claude/settings.json` → `.claude/hooks/require-method-comments.sh`) blocks `git commit` while changed `.cs`/`.razor` files lack its sign-off.
- **Commits:** conventional style (`feat:`, `fix:`, `docs:`, `test:`, `build:`), one commit per task, subject in plain words about what changes for users.
- **New features** start with a design doc in `docs/superpowers/specs/` and a plan in `docs/superpowers/plans/`.
- Work on a branch, never directly on `main`; changes reach `main` through pull requests.
