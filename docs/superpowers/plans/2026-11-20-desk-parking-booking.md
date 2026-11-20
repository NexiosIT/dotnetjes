# Desk & Parking Booking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Blazor Server app where colleagues book desks and parking spots, admins manage resources and people, and a daily summary email goes out — all started with one `docker compose up`.

**Architecture:** One ASP.NET Core (.NET 10) app: `Booking.Core` holds the domain, EF Core data access, booking/admin/summary logic; `Booking.Web` holds Blazor pages, the current-user picker, SMTP sending and the background scheduler. Postgres and Mailpit run beside it in Docker Compose. Three people work in parallel against interfaces + stubs that the skeleton puts in place first.

**Tech Stack:** .NET 10, Blazor Web App (Interactive Server), EF Core 10 + Npgsql, PostgreSQL 17, MailKit, Mailpit, xUnit v2, Testcontainers, FakeTimeProvider, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-11-20-desk-parking-booking-design.md` — read it before your section; the plan argues from it.

## Who does what

| Section | Who | Branch | When |
|---|---|---|---|
| S — Skeleton (S1–S5) | Person A + Claude | `main` | 0:45–1:05 |
| A — Booking rules (A1–A4) | Person A | `feat/booking-rules` | 1:05–3:15 |
| B — Day view (B1–B3) | Person B | `feat/day-view` | 1:05–3:15 |
| C — Admin & email (C1–C5) | Person C | `feat/admin-email` | 1:05–3:15 |
| I — Integration (I1) | Everyone | `main` | 3:15–3:45 |

**Checkpoint at ~2:15:** each person commits, runs `git fetch origin && git merge origin/main`, runs `dotnet test` and `docker compose up --build`, then merges their branch into `main` (PR or direct push — team's choice). Repeat at the end.

**Plan calls that refine the spec** (spec §6 file locations): `AdminService`, the summary composer/sender and the schedule calculation live in `Booking.Core` (not `Booking.Web`) so they can be unit-tested in the one test project; ownership is unchanged. Each person registers services in **their own** `Setup/*Feature.cs` file, so `Program.cs` never changes after the skeleton. `ICurrentUser` gains `ClearAsync()` (Review Focus 2). `Person.Email` is optional (`string?`), matching "every active person *with* an email address".

## Global Constraints

- Target framework `net10.0`. `global.json`: `{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }` (this machine's default SDK is 11).
- Package versions (added **only** in the skeleton): `Microsoft.EntityFrameworkCore` 10.0.15, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `Microsoft.EntityFrameworkCore.Design` 10.0.15, `MailKit` 4.18.2, `Testcontainers.PostgreSql` 4.17.0, `Microsoft.Extensions.TimeProvider.Testing` 10.11.1; local tool `dotnet-ef` 10.0.15.
- Postgres image `postgres:18-alpine` everywhere (compose and Testcontainers); compose data in `.container-data/postgres` (bind mount on `/var/lib/postgresql`). DB `booking`, user `booking`, password `booking` (demo only).
- Ports: app `8080`, Mailpit UI `8025`, Mailpit SMTP `1025`, Postgres `5432`.
- Time zone `Europe/Brussels` (`App:TimeZone`); `Booking:MaxDaysAhead` = 14; `Summary:SendAt` = `07:00`. Weekdays only.
- Index names: `IX_Bookings_Resource_Date`, `IX_Bookings_Person_Type_Date`, `IX_Resources_Code`.
- `Booking.CreatedAt` is always UTC, taken from `TimeProvider`.
- Data access only through `IDbContextFactory<BookingDbContext>`; one context per service method, disposed at the end.
- Whole app renders `InteractiveServerRenderMode(prerender: false)`.
- Only Person A changes entities, `BookingDbContext` and migrations; any model change ships with its migration in the same commit.
- Nobody edits `Program.cs` after S4. Register services in your own `src/Booking.Web/Setup/*Feature.cs`.
- No hard deletes from admin screens; deactivate instead.

## Review Focus

1. **Stale screen:** a user clicks **Book** on a slot someone else took seconds ago → they see "D2 was just taken — refresh to see who." and the view refreshes itself; no error page. *(B3)*
2. **Remembered user no longer valid** (deactivated, or DB reset with `docker compose down -v`) → treated as "not chosen", picker prompts again; no crash. *(B1)*
3. **Midnight in Brussels while the container runs on UTC** → "today" follows Brussels. *(S3)*
4. **Resource codes typed loosely** (`" d1 "` when `D1` exists) → trimmed, upper-cased, rejected as duplicate with a readable message. *(C1)*
5. **SMTP down or one address failing** → "Send now" reports how many sent/failed; the background job logs and keeps running; the app never crashes. *(C4, C5)*

---

## Section S — Skeleton (Person A + Claude, on `main`)

Done when a clean clone runs `docker compose up --build` and shows the (stub) day page. B and C read their sections meanwhile.

### Task S1: Solution, projects, tooling

**Files:**
- Create: `global.json`, `.gitignore` (`dotnet new gitignore`), `Dotnetjes.slnx`, `.config/dotnet-tools.json`
- Create: `src/Booking.Core/Booking.Core.csproj` (classlib), `src/Booking.Web/` (`dotnet new blazor --interactivity Server --all-interactive --no-https -f net10.0`), `tests/Booking.Core.Tests/Booking.Core.Tests.csproj` (`dotnet new xunit -f net10.0`)

**Interfaces:**
- Produces: project references Web → Core, Tests → Core. All packages from Global Constraints: EF Core + Npgsql in Core; EF Design + MailKit in Web; Testcontainers + TimeProvider.Testing in Tests.

- [ ] **Step 1:** Create the files above; delete the template's `Counter.razor` and `Weather.razor` pages and their nav links; `dotnet tool install dotnet-ef --version 10.0.15` after `dotnet new tool-manifest`.
- [ ] **Step 2:** In `src/Booking.Web/Components/App.razor`, set both `<HeadOutlet>` and `<Routes>` to `@rendermode="new InteractiveServerRenderMode(prerender: false)"`.
- [ ] **Step 3: Verify** — Run: `dotnet --version` → `10.0.x`; `dotnet build Dotnetjes.slnx` → `Build succeeded`, 0 errors.
- [ ] **Step 4: Commit** — `git commit -m "chore: solution skeleton"`

### Task S2: Domain model, DbContext, contracts, migration, seed

**Files:**
- Create: `src/Booking.Core/Entities/{Person,Resource,Booking,ResourceType}.cs`
- Create: `src/Booking.Core/Data/BookingDbContext.cs`, `Data/BookingDbContextDesignFactory.cs`, `Data/SeedData.cs`, `Data/Migrations/*` (generated)
- Create: `src/Booking.Core/Services/{IBookingService,DayOverview,ResourceSlot,BookingResult,BookingError}.cs`
- Create: `src/Booking.Core/Admin/{IAdminService,AdminValidationException}.cs`
- Create: `tests/Booking.Core.Tests/Infrastructure/PostgresFixture.cs`, `tests/Booking.Core.Tests/Data/DatabaseTests.cs`

**Interfaces:**
- Produces: entities exactly per spec §5 (`Person.Email` is `string?`); `DbSet`s `People`, `Resources`, `Bookings`; contracts `IBookingService`, `DayOverview`, `ResourceSlot`, `BookingResult`, `BookingError`, `IAdminService`, `AdminValidationException` **exactly as spec §5**.
- Produces: `static Task SeedData.EnsureSeededAsync(BookingDbContext db, CancellationToken ct = default)` — only when `People` is empty: desks `D1`–`D8` named `Desk 1`…`Desk 8`, parking `P1`–`P4` named `Parking 1`…`Parking 4`, people `Anna, Ben, Chris, Dana, Eli, Fien` with `<lowercase name>@example.com`, all active.
- Produces (tests): `PostgresFixture : IAsyncLifetime` shared through `[CollectionDefinition("postgres")] PostgresCollection : ICollectionFixture<PostgresFixture>` — one `postgres:18-alpine` container per test run; `Task<IDbContextFactory<BookingDbContext>> CreateDatabaseAsync()` creates a uniquely named database, runs `MigrateAsync`, returns `new PooledDbContextFactory<BookingDbContext>(options)`.

- [ ] **Step 1: Write the failing tests** in `DatabaseTests` (`[Collection("postgres")]`):

```csharp
[Fact] Migrations_apply_and_seed_runs_once()
// CreateDatabaseAsync; EnsureSeededAsync twice
// Assert: Resources.Count(Desk) == 8, Resources.Count(Parking) == 4, People.Count() == 6

[Fact] Unique_indexes_exist_with_expected_names()
// Assert pg_indexes contains "IX_Bookings_Resource_Date", "IX_Bookings_Person_Type_Date", "IX_Resources_Code"
```

- [ ] **Step 2: Run** `dotnet test tests/Booking.Core.Tests` → FAIL (types missing / no migration).
- [ ] **Step 3: Implement** entities, `BookingDbContext(DbContextOptions<BookingDbContext>)` with the three named unique indexes (`Booking(ResourceId, Date)`, `Booking(PersonId, ResourceType, Date)`, `Resource(Code)`), the design-time factory (connection string `Host=localhost;Port=5432;Database=booking;Username=booking;Password=booking`), contracts and `SeedData`. Generate: `dotnet ef migrations add InitialCreate --project src/Booking.Core --startup-project src/Booking.Web --output-dir Data/Migrations`.
- [ ] **Step 4: Run** `dotnet test tests/Booking.Core.Tests` (Docker running) → 2 passed.
- [ ] **Step 5: Commit** — `git commit -m "feat: domain model, contracts, initial migration, seed"`

### Task S3: Office calendar

**Files:**
- Create: `src/Booking.Core/Calendar/{IOfficeCalendar,OfficeCalendar}.cs`
- Test: `tests/Booking.Core.Tests/Calendar/OfficeCalendarTests.cs`

**Interfaces:**
- Produces: `IOfficeCalendar` exactly as spec §5; `OfficeCalendar(TimeProvider time, TimeZoneInfo zone, int maxDaysAhead)`.

- [ ] **Step 1: Write the failing tests** (Brussels = `TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels")`, max 14, `FakeTimeProvider`):

```csharp
Today_is_computed_in_brussels()          // now 2026-11-20T23:30Z → Today == 2026-11-21
FirstBookableDay_is_today_on_weekday()   // now 2026-11-18T08:00Z → 2026-11-18
FirstBookableDay_skips_weekend()         // now 2026-11-21T10:00Z → 2026-11-23
LastBookableDay_is_today_plus_max()      // now 2026-11-18T08:00Z → 2026-12-02
[Theory] IsBookable(date, expected)      // now 2026-11-18T08:00Z:
                                         // 11-17 false, 11-18 true, 11-21 false, 12-02 true, 12-03 false
Weekday_navigation_skips_weekend()       // NextWeekday(2026-11-20) == 2026-11-23; PreviousWeekday(2026-11-23) == 2026-11-20
```

- [ ] **Step 2: Run** `dotnet test --filter "FullyQualifiedName~OfficeCalendarTests"` → FAIL.
- [ ] **Step 3: Implement** `OfficeCalendar` — `Today` converts `time.GetUtcNow()` with `TimeZoneInfo.ConvertTime`.
- [ ] **Step 4: Run** same filter → all pass.
- [ ] **Step 5: Commit** — `git commit -m "feat: office calendar"`

### Task S4: Web wiring, stubs, empty pages

**Files:**
- Modify: `src/Booking.Web/Program.cs`, `appsettings.json`, `Components/Layout/NavMenu.razor`, `Components/Layout/MainLayout.razor`
- Create: `src/Booking.Web/Setup/{BookingFeature,DayViewFeature,AdminFeature}.cs`
- Create: `src/Booking.Web/Stubs/{StubData,StubBookingService,StubAdminService}.cs`
- Create: `src/Booking.Web/CurrentUser/{ICurrentUser,InMemoryCurrentUser}.cs`
- Create: `src/Booking.Web/Components/Layout/UserPicker.razor` (placeholder text "You: —"), `Components/Pages/Day.razor` (`@page "/"`, heading only), `Components/Pages/Admin/Resources.razor` (`/admin/resources`), `Components/Pages/Admin/People.razor` (`/admin/people`)
- Create: `docker-compose.yml` with `db` and `mailpit` services (spec §7 Docker details)

**Interfaces:**
- Produces: `ICurrentUser { Task<int?> GetPersonIdAsync(); Task SetAsync(int personId); Task ClearAsync(); event Action? Changed; }` — `InMemoryCurrentUser` keeps the id in a field.
- Produces: `AddBookingFeature(this IServiceCollection)` → `StubBookingService` (singleton); `AddDayViewFeature(this IServiceCollection)` → `InMemoryCurrentUser` (scoped); `AddAdminFeature(this IServiceCollection, IConfiguration)` → `StubAdminService` (singleton).
- Produces: stubs share `StubData`: people Anna(1), Ben(2), Chris(3); desks D1–D3, parking P1–P2. `StubBookingService` keeps bookings in memory and returns `ResourceTaken` for a taken slot; `StubAdminService` add/update work in memory.
- Produces: `Program.cs` registers Razor components, `AddDbContextFactory<BookingDbContext>(UseNpgsql(GetConnectionString("Booking")))`, `TimeProvider.System`, `IOfficeCalendar` (singleton, from `App:TimeZone` and `Booking:MaxDaysAhead`), the three `Add*Feature` calls, then on startup: create a context, `MigrateAsync`, `SeedData.EnsureSeededAsync`.
- Produces: `appsettings.json` keys: `ConnectionStrings:Booking` (localhost, as S2), `App:TimeZone`, `Booking:MaxDaysAhead`, `Summary:SendAt`, `Smtp:Host` `localhost`, `Smtp:Port` `1025`, `Smtp:From` `booking@example.com`.

- [ ] **Step 1: Implement** the files above. Nav: "Book" (`/`), "Admin" (`/admin/resources`). `UserPicker` sits in the `MainLayout` top row.
- [ ] **Step 2: Verify** — `docker compose up -d db mailpit`; `dotnet run --project src/Booking.Web`; open the URL printed → "Book" page renders, nav links work, no errors in console. `docker compose exec db psql -U booking -d booking -c 'select count(*) from "Resources";'` → `12`.
- [ ] **Step 3: Commit** — `git commit -m "feat: web wiring with stubs and empty pages"`

### Task S5: Dockerfile and app container

**Files:**
- Create: `Dockerfile`, `.dockerignore` (`bin/`, `obj/`, `tests/`, `.git/`)
- Modify: `docker-compose.yml` (add `app`)

**Interfaces:**
- Produces: multi-stage build from `mcr.microsoft.com/dotnet/sdk:10.0` (copy `global.json`, restore + publish `src/Booking.Web/Booking.Web.csproj` only) to `mcr.microsoft.com/dotnet/aspnet:10.0`; `ASPNETCORE_HTTP_PORTS=8080`.
- Produces: compose `app`: `build: .`, `ports: 8080:8080`, `depends_on: db: condition: service_healthy`, env `ConnectionStrings__Booking=Host=db;Port=5432;Database=booking;Username=booking;Password=booking`, `Smtp__Host=mailpit`. `db` (`postgres:18-alpine`) has TCP healthcheck `pg_isready -h 127.0.0.1 -U booking -d booking` and bind mount `./.container-data/postgres:/var/lib/postgresql`; app keys on volume `dpkeys:/keys` (`DataProtection__KeysPath=/keys`).

- [ ] **Step 1: Implement** the files above.
- [ ] **Step 2: Verify** — `docker compose up --build -d`; `curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/` → `200`; `http://localhost:8025` → Mailpit UI. `docker compose down` then `docker compose up -d` → app starts again, seed not duplicated (`select count(*) from "People"` → `6`).
- [ ] **Step 3: Commit and push** — `git commit -m "feat: dockerize app"`; push `main` (confirm with the team first). Tell B and C to pull.

---

## Section A — Booking rules (Person A, `feat/booking-rules`)

All tests: `[Collection("postgres")]`, fresh database per test, seeded, clock frozen at **2026-11-18T08:00Z** (Wed 09:00 Brussels → today `2026-11-18`, last bookable `2026-12-02`).

### Task A1: Test harness + `GetDayAsync`

**Files:**
- Create: `tests/Booking.Core.Tests/Booking/BookingTestContext.cs`, `tests/Booking.Core.Tests/Booking/GetDayTests.cs`
- Create: `src/Booking.Core/Services/BookingService.cs`

**Interfaces:**
- Consumes: `PostgresFixture.CreateDatabaseAsync()`, `SeedData.EnsureSeededAsync`, `OfficeCalendar`.
- Produces: `BookingService(IDbContextFactory<BookingDbContext> dbFactory, IOfficeCalendar calendar, TimeProvider time) : IBookingService`.
- Produces (tests): `BookingTestContext.CreateAsync(PostgresFixture)` exposing `Service`, `DbFactory`, `Today` (`2026-11-18`), `int Person(string name)`, `int Resource(string code)`, `Task DeactivateResource(string code)`, `Task DeactivatePerson(string name)`.

- [ ] **Step 1: Write the failing tests:**

```csharp
GetDay_lists_active_resources_ordered_by_code()
// Desks.Count == 8, Desks[0].Code == "D1", Parking.Count == 4, all BookingId == null, PeopleInOffice empty
GetDay_shows_bookings_and_distinct_people_in_office()
// Anna books D2 and P2 today → slot D2.BookedByName == "Anna", BookingId != null; PeopleInOffice == ["Anna"]
GetDay_hides_inactive_free_resource_keeps_inactive_booked_one()
// Ben books D3; deactivate D3 and D4 → Desks contains D3 (BookedByName "Ben"), not D4
```

- [ ] **Step 2: Run** `dotnet test --filter "FullyQualifiedName~GetDayTests"` → FAIL.
- [ ] **Step 3: Implement** `GetDayAsync`; `BookAsync`/`CancelAsync` throw `NotImplementedException` for now (tests may insert bookings directly through `DbFactory`).
- [ ] **Step 4: Run** same filter → pass.
- [ ] **Step 5: Commit** — `git commit -m "feat: day overview"`

### Task A2: `BookAsync` validation

**Files:**
- Modify: `src/Booking.Core/Services/BookingService.cs`
- Test: `tests/Booking.Core.Tests/Booking/BookValidationTests.cs`

**Interfaces:**
- Consumes: `BookingTestContext` (A1).
- Produces: check order — NotFound (person/resource) → OutOfRange → Weekend → PersonInactive → ResourceInactive.

- [ ] **Step 1: Write the failing tests:**

```csharp
Book_succeeds_and_stores_type_and_utc_timestamp()
// Anna, P1, today → Success, BookingId > 0; stored ResourceType == Parking; CreatedAt == 2026-11-18T08:00Z, Kind Utc
[Theory] Book_rejects_out_of_range(date)   // 2026-11-17, 2026-12-03 → OutOfRange
Book_rejects_weekend()                    // 2026-11-21 → Weekend
Book_rejects_inactive_person()            // deactivate Ben → PersonInactive
Book_rejects_inactive_resource()          // deactivate D5 → ResourceInactive
Book_rejects_unknown_ids()                // personId 9999 → NotFound; resourceId 9999 → NotFound
```

- [ ] **Step 2: Run** `--filter "FullyQualifiedName~BookValidationTests"` → FAIL.
- [ ] **Step 3: Implement** the checks; `CreatedAt = time.GetUtcNow().UtcDateTime`.
- [ ] **Step 4: Run** → pass.
- [ ] **Step 5: Commit** — `git commit -m "feat: booking validation rules"`

### Task A3: Conflicts and concurrency

**Files:**
- Modify: `src/Booking.Core/Services/BookingService.cs`
- Test: `tests/Booking.Core.Tests/Booking/BookConflictTests.cs`

**Interfaces:**
- Produces: pre-check queries for both conflicts, plus catch `DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }` mapped by `ConstraintName` (`IX_Bookings_Resource_Date` → `ResourceTaken`, `IX_Bookings_Person_Type_Date` → `AlreadyBookedThatType`).

- [ ] **Step 1: Write the failing tests:**

```csharp
Book_rejects_taken_resource()                   // Anna D2 today; Ben D2 today → ResourceTaken
Book_rejects_second_desk_same_day()             // Ben D3, then D4 same day → AlreadyBookedThatType
Book_allows_desk_and_parking_same_day()         // Ben D3 + P1 → both Success
Book_allows_same_resource_on_other_day()        // Anna D2 on 11-18, Ben D2 on 11-19 → both Success
Concurrent_bookings_of_one_desk_give_one_winner()
// all 6 people book D1 today via Task.WhenAll → exactly 1 Success, 5 ResourceTaken, no exception
Concurrent_bookings_by_one_person_give_one_desk()
// Anna books D1..D6 today via Task.WhenAll → exactly 1 Success, 5 AlreadyBookedThatType
```

- [ ] **Step 2: Run** `--filter "FullyQualifiedName~BookConflictTests"` → FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** → pass (run twice; concurrency tests must be stable).
- [ ] **Step 5: Commit** — `git commit -m "feat: booking conflicts enforced by database"`

### Task A4: `CancelAsync` + switch the app to the real service

**Files:**
- Modify: `src/Booking.Core/Services/BookingService.cs`, `src/Booking.Web/Setup/BookingFeature.cs`
- Delete: `src/Booking.Web/Stubs/StubBookingService.cs` (keep `StubData` if C's stub still uses it)
- Test: `tests/Booking.Core.Tests/Booking/CancelTests.cs`

**Interfaces:**
- Produces: `AddBookingFeature` registers `BookingService` as scoped `IBookingService`.

- [ ] **Step 1: Write the failing tests:**

```csharp
Cancel_removes_own_booking()        // Anna books D2, cancels → Success; GetDay: D2.BookingId == null
Cancel_rejects_other_persons()      // Anna's booking, Ben cancels → NotYourBooking
Cancel_unknown_booking()            // bookingId 9999 → NotFound
Cancel_rejects_past_booking()       // booking inserted directly for 2026-11-17 → OutOfRange
```

- [ ] **Step 2: Run** `--filter "FullyQualifiedName~CancelTests"` → FAIL.
- [ ] **Step 3: Implement** `CancelAsync`; swap the registration; delete the stub.
- [ ] **Step 4: Verify** — `dotnet test` → all green; `docker compose up --build -d` → day page shows `D1`–`D8`, `P1`–`P4` from the database.
- [ ] **Step 5: Commit** — `git commit -m "feat: cancel booking, use real booking service"`

---

## Section B — Day view (Person B, `feat/day-view`)

UI tasks are verified by running the app (`docker compose up -d db mailpit` + `dotnet run --project src/Booking.Web`) against the stubs until A and C merge. Use two browser windows (one normal, one private) to act as two people.

### Task B1: Remember who you are

**Files:**
- Create: `src/Booking.Web/CurrentUser/LocalStorageCurrentUser.cs`
- Modify: `src/Booking.Web/Components/Layout/UserPicker.razor`, `src/Booking.Web/Setup/DayViewFeature.cs`
- Delete: `src/Booking.Web/CurrentUser/InMemoryCurrentUser.cs`

**Interfaces:**
- Consumes: `IAdminService.GetPeopleAsync()` (active people only shown), `ICurrentUser`.
- Produces: `LocalStorageCurrentUser(IJSRuntime js) : ICurrentUser`, scoped; key `booking.personId`; reads via `js.InvokeAsync<string?>("localStorage.getItem", ...)` once and caches; `SetAsync`/`ClearAsync` write/remove and raise `Changed`.
- Produces: `UserPicker` shows `You: <name> ▾`; with no valid choice shows `Pick who you are ▾`. If the stored id is not among active people → `ClearAsync()` (Review Focus 2).

- [ ] **Step 1: Implement** the above; register in `AddDayViewFeature`.
- [ ] **Step 2: Verify** — pick Anna → header reads "You: Anna ▾"; reload → still Anna; in browser dev tools set `localStorage['booking.personId'] = '999'`, reload → "Pick who you are ▾", no error.
- [ ] **Step 3: Commit** — `git commit -m "feat: current user picker remembered in browser"`

### Task B2: Day view display and navigation

**Files:**
- Modify: `src/Booking.Web/Components/Pages/Day.razor` (+ `Day.razor.css` if needed)
- Create: `src/Booking.Web/Components/Pages/SlotRow.razor`

**Interfaces:**
- Consumes: `IBookingService.GetDayAsync`, `IOfficeCalendar` (`FirstBookableDay`, `LastBookableDay`, `NextWeekday`, `PreviousWeekday`, `IsBookable`), `ICurrentUser.Changed`.
- Produces: page opens on `FirstBookableDay`; title formatted `ddd d MMM` (invariant culture, e.g. `Fri 20 Nov`); ◀ disabled when `PreviousWeekday(date) < FirstBookableDay`, ▶ disabled when `NextWeekday(date) > LastBookableDay`. Columns "DESKS" and "PARKING"; row states: free → `free [Book]`; booked by current user → `You [Cancel]`; otherwise → `taken by <name>`. Footer `In the office: <names joined by ", ">` or `Nobody booked yet.`
- Produces: subscribes to `ICurrentUser.Changed` (re-render via `InvokeAsync(StateHasChanged)`), unsubscribes in `Dispose`.

- [ ] **Step 1: Implement.** Book/Cancel buttons render but do nothing yet; Book disabled when no current user, with the line "Pick who you are to book."
- [ ] **Step 2: Verify** — against stub data: arrows step Mon→Fri skipping weekends and stop at the range ends; switching user in the header updates "You" rows without reload.
- [ ] **Step 3: Commit** — `git commit -m "feat: day view with weekday navigation"`

### Task B3: Book, cancel, and plain-language errors

**Files:**
- Create: `src/Booking.Web/Components/Pages/BookingMessages.cs`
- Modify: `src/Booking.Web/Components/Pages/Day.razor`, `SlotRow.razor`

**Interfaces:**
- Consumes: `IBookingService.BookAsync/CancelAsync`, `BookingResult`, `BookingError`.
- Produces: `static string BookingMessages.For(BookingError error, ResourceSlot slot)` with exactly this copy:

| Error | Message |
|---|---|
| `OutOfRange` | `That day can't be booked.` |
| `Weekend` | `The office is closed at weekends.` |
| `ResourceTaken` | `{Code} was just taken — refresh to see who.` |
| `AlreadyBookedThatType` | `You already have a desk that day.` / `You already have a parking spot that day.` |
| `PersonInactive` | `Your profile is inactive — ask an admin.` |
| `ResourceInactive` | `{Code} is no longer available.` |
| `NotFound` | `That no longer exists — the page has been refreshed.` |
| `NotYourBooking` | `You can only cancel your own bookings.` |

- [ ] **Step 1: Implement** — after **every** Book/Cancel (success or not) reload the overview; show the message above the columns until the next action or day change; disable buttons while a call is running (no double submit).
- [ ] **Step 2: Verify (Review Focus 1)** — window 1 as Anna, window 2 as Ben, both on the same day. Anna books P2. Ben (stale view, P2 still "free") clicks Book on P2 → message `P2 was just taken — refresh to see who.` and the row now shows `taken by Anna`. Ben books D3 then D4 → `You already have a desk that day.` Anna cancels P2 → row shows `free [Book]`.
- [ ] **Step 3: Commit** — `git commit -m "feat: book and cancel from the day view"`

---

## Section C — Admin & email (Person C, `feat/admin-email`)

### Task C1: Real `AdminService`

**Files:**
- Create: `src/Booking.Core/Admin/AdminService.cs`
- Modify: `src/Booking.Web/Setup/AdminFeature.cs`
- Delete: `src/Booking.Web/Stubs/StubAdminService.cs`
- Test: `tests/Booking.Core.Tests/Admin/AdminServiceTests.cs` (`[Collection("postgres")]`, fresh seeded database per test)

**Interfaces:**
- Consumes: `IAdminService`, `AdminValidationException` (S2), `PostgresFixture`, `SeedData`.
- Produces: `AdminService(IDbContextFactory<BookingDbContext> dbFactory) : IAdminService`, scoped. Codes trimmed + upper-cased; names and emails trimmed; blank email stored as `null`. Resources ordered by `Type`, then `Code`; people by `Name`. Duplicate code: pre-check, plus `23505` on `IX_Resources_Code` mapped to the same exception.

- [ ] **Step 1: Write the failing tests:**

```csharp
AddResource_normalizes_code()          // AddResourceAsync(" d9 ", " Desk 9 ", Desk) → Code "D9", Name "Desk 9"
AddResource_rejects_duplicate_code()   // AddResourceAsync(" d1 ", "x", Desk) → AdminValidationException "A resource with code D1 already exists."
AddResource_requires_code_and_name()   // "" code → "Code is required."; "" name → "Name is required."
UpdateResource_toggles_active()        // UpdateResourceAsync(P4, "P4", "Parking 4", false) → GetResourcesAsync: P4.IsActive == false
Update_unknown_id_fails()              // id 9999 → "That item no longer exists."
AddPerson_validates_email()            // ("Gert", "gert") → "Email must contain '@'."; ("Gert", "") → stored Email == null
UpdatePerson_accepts_null_email()      // UpdatePersonAsync(<Dana id>, "Dana", null, true) → stored Email == null, no exception
GetPeople_ordered_by_name()            // first == "Anna", last == "Fien"
```

- [ ] **Step 2: Run** `--filter "FullyQualifiedName~AdminServiceTests"` → FAIL.
- [ ] **Step 3: Implement**; swap the registration; delete `StubAdminService.cs` (and the whole `Stubs/` folder if `StubBookingService.cs` is already gone).
- [ ] **Step 4: Run** → pass.
- [ ] **Step 5: Commit** — `git commit -m "feat: admin service"`

### Task C2: Admin — resources page

**Files:**
- Modify: `src/Booking.Web/Components/Pages/Admin/Resources.razor`
- Create: `src/Booking.Web/Components/Pages/Admin/AdminTabs.razor` (links: Resources | People | summary button slot)

**Interfaces:**
- Consumes: `IAdminService` resource methods, `AdminValidationException`.
- Produces: table Code | Name | Type | Active (checkbox) | Edit; add form (code, name, type select); inline edit of code/name; toggling Active saves immediately. Every `AdminValidationException` shows its message above the table; nothing else is caught.

- [ ] **Step 1: Implement.**
- [ ] **Step 2: Verify** — add `D9` → appears; add `d9` → "A resource with code D9 already exists."; untick P4 → on `/`, P4 no longer offered (once A's service is merged; with stubs check the table only).
- [ ] **Step 3: Commit** — `git commit -m "feat: admin resources page"`

### Task C3: Admin — people page

**Files:**
- Modify: `src/Booking.Web/Components/Pages/Admin/People.razor`

**Interfaces:**
- Consumes: `IAdminService` people methods.
- Produces: same layout as C2 with columns Name | Email | Active | Edit.

- [ ] **Step 1: Implement.**
- [ ] **Step 2: Verify** — add "Gert" with `gert@example.com` → appears and shows in the header picker after reload; email `gert` → "Email must contain '@'."; deactivate Gert → gone from the picker.
- [ ] **Step 3: Commit** — `git commit -m "feat: admin people page"`

### Task C4: Summary email content and sending

**Files:**
- Create: `src/Booking.Core/Notifications/{SummaryEmail,IEmailSender,DailySummaryComposer,DailySummarySender,SummaryResult}.cs`
- Test: `tests/Booking.Core.Tests/Notifications/DailySummaryComposerTests.cs`, `DailySummarySenderTests.cs` (no database; hand-written fakes)

**Interfaces:**
- Consumes: `IBookingService.GetDayAsync`, `IAdminService.GetPeopleAsync`, `IOfficeCalendar.Today`.
- Produces: `record SummaryEmail(string To, string Subject, string HtmlBody)`; `interface IEmailSender { Task SendAsync(SummaryEmail email, CancellationToken ct); }`; `static SummaryEmail DailySummaryComposer.Compose(DayOverview day, Person recipient)`; `record SummaryResult(int Sent, int Failed)`; `DailySummarySender(IBookingService, IAdminService, IOfficeCalendar, IEmailSender, ILogger<DailySummarySender>)` with `Task<SummaryResult> SendTodayAsync(CancellationToken ct)`.

- [ ] **Step 1: Write the failing tests:**

```csharp
Compose_subject_names_the_day()       // day 2026-11-20 → Subject == "Who's in the office – Fri 20 Nov"
Compose_lists_people_and_spots()      // Anna D2+P2, Ben D3 → body contains "Anna", "D2", "P2", "Ben", "D3"
Compose_counts_free()                 // 8 desks/4 parking, 2 desks + 1 parking booked → body contains "Free: 6 desks, 3 parking spots"
Compose_empty_day()                   // no bookings → body contains "Nobody has booked for today."
Compose_encodes_names()               // person "Tom & Jerry" → body contains "Tom &amp; Jerry"
Send_goes_to_each_active_person_with_email()
// people: Anna(active, email), Ben(active, email), Chris(inactive), Dana(active, no email) → Sent == 2, two calls, To matches
Send_survives_a_failing_address()     // fake sender throws for Ben → Sent == 1, Failed == 1, no exception (Review Focus 5)
```

- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Notifications"` → FAIL.
- [ ] **Step 3: Implement** — date text with `CultureInfo.InvariantCulture`; names via `WebUtility.HtmlEncode`; one email per recipient; log and count each failure.
- [ ] **Step 4: Run** → pass.
- [ ] **Step 5: Commit** — `git commit -m "feat: daily summary composition and sending"`

### Task C5: SMTP, 07:00 schedule, "Send now" button

**Files:**
- Create: `src/Booking.Core/Notifications/SummarySchedule.cs`
- Create: `src/Booking.Web/Notifications/{SmtpEmailSender,DailySummaryService}.cs`
- Modify: `src/Booking.Web/Setup/AdminFeature.cs`, `src/Booking.Web/Components/Pages/Admin/AdminTabs.razor`
- Test: `tests/Booking.Core.Tests/Notifications/SummaryScheduleTests.cs`

**Interfaces:**
- Produces: `static DateTimeOffset SummarySchedule.NextRunUtc(DateTimeOffset nowUtc, TimeZoneInfo zone, TimeOnly sendAt)` — next weekday at `sendAt` local time, strictly after `now`.
- Produces: `SmtpEmailSender(IOptions<SmtpOptions>) : IEmailSender` (MailKit, no auth, no TLS; `SmtpOptions { Host, Port, From }` bound from `Smtp`); `DailySummaryService(IServiceScopeFactory, TimeProvider, IConfiguration, ILogger) : BackgroundService` — waits until `NextRunUtc`, creates a scope, calls `DailySummarySender.SendTodayAsync`, catches and logs everything, loops.
- Produces: `AddAdminFeature` registers `SmtpOptions`, `IEmailSender`, `DailySummarySender` (scoped), `DailySummaryService` (hosted).
- Produces: button **Send today's summary now** in `AdminTabs` → shows `Sent {Sent} emails.` or `Sent {Sent}, failed {Failed} — check the logs.`

- [ ] **Step 1: Write the failing tests** (Brussels, 07:00):

```csharp
NextRun_later_today()          // now 2026-11-18T05:00Z (06:00 local) → 2026-11-18T06:00Z
NextRun_at_exact_time_is_next_day() // now 2026-11-18T06:00Z → 2026-11-19T06:00Z
NextRun_skips_weekend()        // now 2026-11-20T08:00Z (Fri) → 2026-11-23T06:00Z
NextRun_from_saturday()        // now 2026-11-21T10:00Z → 2026-11-23T06:00Z
```

- [ ] **Step 2: Run** `--filter "FullyQualifiedName~SummaryScheduleTests"` → FAIL.
- [ ] **Step 3: Implement** the schedule, sender, background service, registrations and button.
- [ ] **Step 4: Verify** — tests pass; run app, click the button → Mailpit (`http://localhost:8025`) shows one email per active person with an email. `docker compose stop mailpit`, click again → "failed" message, app keeps working (Review Focus 5). `docker compose start mailpit`.
- [ ] **Step 5: Commit** — `git commit -m "feat: scheduled and on-demand summary email"`

---

## Section I — Integration (everyone, `main`)

### Task I1: Demo run on a clean clone

- [ ] **Step 1:** All branches merged to `main`; `dotnet test` → all green.
- [ ] **Step 2:** In a fresh folder: `git clone https://github.com/NexiosIT/dotnetjes.git && cd dotnetjes && docker compose down -v; docker compose up --build`.
- [ ] **Step 3:** Run spec §9 steps 1–6 exactly. Each step must pass without touching code.
- [ ] **Step 4:** Fix anything that fails (owner of the failing area fixes; rerun from step 2), commit, push.
