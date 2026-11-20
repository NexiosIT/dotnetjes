# CLAUDE.md

Desk & parking booking app: .NET 10, Blazor Web App (Interactive Server, `prerender: false`), EF Core 10 + Npgsql/PostgreSQL, Keycloak (OIDC) login, Tailwind CSS v4, xUnit v2. See README.md for running and configuration.

## Commands

```bash
dotnet build                      # also compiles Tailwind (Styles/app.css → wwwroot/app.css)
dotnet test                       # integration tests need Docker running (Testcontainers Postgres)
dotnet test tests/Booking.Domain.Tests           # fast, no Docker
dotnet test tests/Booking.IntegrationTests --filter "FullyQualifiedName~SwitchTests"
dotnet ef migrations add <Name> --project src/Booking.Infrastructure --startup-project src/Booking.Web --output-dir Persistence/Migrations
docker compose up --build         # app :8080, Mailpit UI :8025
```

`dotnet run` needs the Keycloak settings in user-secrets (project `src/Booking.Web`); without them the app refuses to start.

## Architecture

DDD with a clean-architecture split. Dependencies point inwards only: `Web → Infrastructure → Application → Domain`.

- **`Booking.Domain`**: aggregates that enforce their own rules, with private setters and factories: `Booking.Book` / `CanBeCancelledBy`, `Resource.Create` / `Update` / `SetParkingDetails` / `IsChargerSpot`, `Person.FromLogin` / `RefreshProfile` / `SetActive`, `WaitlistEntry.Join`. Refusals return `Result<T, TError>`; invalid admin input throws `DomainValidationException`. Also `IOfficeCalendar` and the `ChargerSpotFreed` event. No framework references. Tests that need raw rows (past dates) use the `internal` `Reconstitute` methods.
- **`Booking.Application`**: the use cases (`BookingService`, `WaitlistService`, `AdminService`, `PersonDirectory`, email senders) and the ports they need in `Abstractions/`: `IUnitOfWorkFactory` (one unit of work per operation; Blazor Server circuits are long-lived), one repository per aggregate, `IDayOverviewQuery`, `IEmailSender`. Rules that span a whole day (one booking per spot, one of each type per person, charger availability) are checked here. The UI gets read models (`DayOverview`, `ResourceView`, `PersonView`), never entities. Registered by `AddApplication()`.
- **`Booking.Infrastructure`**: EF Core + PostgreSQL (`Persistence/`: DbContext, migrations, seed, repositories, `DayOverviewQuery`), SMTP (`Email/`), background jobs (`BackgroundJobs/`). Registered by `AddInfrastructure(configuration)`; `InitializeDatabaseAsync()` migrates and seeds at startup.
- **`Booking.Web`**: Blazor UI, Keycloak login (`Setup/AuthFeature.cs`), `ICurrentUser`, and the composition root `Program.cs`. It never touches EF Core.
- **Races are decided by unique indexes** (`IX_Bookings_Resource_Date`, `IX_Bookings_Person_Type_Date`, `IX_Resources_Code`, `IX_People_ExternalId`, `IX_Waitlist_Person_Date`). Use cases pre-check for a clean error; `EfUnitOfWork.SaveChangesAsync` turns a violated index into a `SaveConflict` value for simultaneous clicks. Keep both when changing booking code.
- **"Today" and bookable days** come from `IOfficeCalendar` (office time zone, weekdays, `Booking:MaxDaysAhead`). Never use `DateTime.Now`; inject `TimeProvider`.
- **The current user** comes from `ICurrentUser` (resolved from the Keycloak `sub` claim). Admin pages are everything under `Components/Pages/Admin/`, locked by `_Imports.razor` with the `"Admin"` policy (Keycloak client role `Admin`).
- **Emails** that react to a user action go through `IWaitlistNotifier` → in-memory channel → `BackgroundService`, so a click never waits on SMTP. One failing address is logged and never stops the others.
- Results are values, not exceptions: `BookingResult` / `BookingError`, `WaitlistResult` / `WaitlistError`. User-facing text for each error lives in `BookingMessages` / `WaitlistMessages`; keep it plain language.

## Conventions

- **TDD.** Write the failing test first. A rule about one aggregate gets a fast test in `Booking.Domain.Tests`; use cases are tested against real Postgres in `Booking.IntegrationTests` via `BookingTestContext` (frozen clock: today is Wed 2026-11-18; seeded desks D1–D8, parking P1–P4 with chargers on P1 and P3; demo people Anna, Ben, Chris, Dana, Eli, Fien). Logic that would sit in a `.razor` file goes in a small static class next to it (`SlotFilter`, `ChargerWaitlist`) so it can be unit-tested.
- **Test names** follow Gherkin: `GivenSituation_WhenAction_ThenOutcome` (PascalCase inside each part, underscores only between parts; leave out `Given…_` when there's no meaningful setup, join extra clauses with `And`). Example: `GivenTheTargetSpotIsTaken_WhenSwitching_ThenTheOldBookingIsKept`.
- **Tailwind:** write full class names in markup or C# string constants. Never build them from pieces (`$"bg-{accent}-500"`): the Tailwind scanner wouldn't find them. Rebuild after adding classes.
- **Secrets** never go in appsettings or git: user-secrets for `dotnet run`, `.env` (gitignored) for Docker.
- **No comments inside method bodies.** Put the why in the XML doc comment (`///`) on the member, or above a field/class. Before every commit, run the `remove-method-comments` skill on the changed files: a `PreToolUse` hook (`.claude/settings.json` → `.claude/hooks/require-method-comments.sh`) blocks `git commit` while changed `.cs`/`.razor` files lack its sign-off.
- **Commits:** conventional style (`feat:`, `fix:`, `docs:`, `test:`, `build:`), one commit per task, subject in plain words about what changes for users.
- **New features** start with a design doc in `docs/superpowers/specs/` and a plan in `docs/superpowers/plans/`.
- Work on a branch, never directly on `main`; changes reach `main` through pull requests.
