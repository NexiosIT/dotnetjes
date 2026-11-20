# Desk & Parking Booking — Design

- **Date:** 2026-11-20
- **Status:** Draft, awaiting team approval
- **Repo:** `NexiosIT/dotnetjes`

## 1. Intent

Three colleagues have a 4-hour session to test the *superpowers* workflow (brainstorm → plan → build with subagents → review) on real work. The outcome must be **one thing that actually works**: a desk and parking booking app for the office, built in parallel by three people.

It is a **working demo** today, but it must run in **Docker** so it can be deployed later without rework.

**Success at the end of the session:** on a clean clone, `docker compose up --build` starts everything, and the demo script in §9 runs start to finish without touching code.

## 2. Goals, non-goals, anti-goals

**Goals**
- Colleagues book a desk and/or a parking spot for a day and see who is in the office.
- Booking rules are enforced reliably (no double bookings, even under concurrent clicks).
- An admin manages desks, parking spots and people.
- A daily summary email goes out every weekday morning.
- Runs entirely via Docker Compose with sample data, persisting across restarts.

**Non-goals (today)**
- Real authentication. Users pick their name from a list. Adding Microsoft (Entra ID) login later must not require rebuilding — the current user is reached only through one abstraction (`ICurrentUser`).
- Protecting admin screens (anyone can open `/admin`).
- Half-day or recurring bookings, a parking waitlist, public holidays, a graphical floor map.
- Hosting, CI/CD, production hardening.
- Catching up on a summary email missed because the app was down at send time.

**Anti-goals (failure even if it "works")**
- It runs only on one laptop and fails on a clean clone.
- Rules enforced only in the UI, so two fast clicks can double-book.
- Three parts built in isolation that don't fit together at the last minute.

## 3. Constraints

- .NET 10 (LTS; SDK 10.0.403 is installed). Blazor for the UI.
- PostgreSQL (team decision), via EF Core + Npgsql.
- Email via SMTP to **Mailpit** (a test mailbox with a web UI) in Docker.
- Docker Desktop required on all three machines (also used by the tests).
- ~2.5 hours of build time per person after design, planning and skeleton.

## 4. Approaches considered

| Approach | Trade-off | Decision |
|---|---|---|
| **A. One ASP.NET Core app, one app container** (+ Postgres + Mailpit containers). Blazor UI, booking logic and email job in one process. Team splits by project/folder. | Fewest moving parts, fastest to a working demo. Needs a shared skeleton first so people don't collide. | **Picked** |
| B. Separate API, front-end and worker containers talking over HTTP. | Clean ownership per container, but time goes into HTTP contracts, CORS, three Dockerfiles and cross-container debugging. Overkill for a demo. | Rejected |
| .NET Aspire for orchestration. | Nice local dev experience, but a second new thing to learn in a 4-hour window. | Dropped |
| SQLite instead of Postgres. | Simpler, no DB container. | Replaced by Postgres (team decision) |

## 5. Domain and rules

### Entities (int IDs)

- **Person** — `Id`, `Name`, `Email`, `IsActive`
- **Resource** — `Id`, `Code` (e.g. `D1`, `P3`, unique), `Name`, `Type` (`Desk` | `Parking`), `IsActive`
- **Booking** — `Id`, `PersonId`, `ResourceId`, `ResourceType` (copied from the resource, needed for the unique index), `Date` (`DateOnly`), `CreatedAt` (`DateTime`, always UTC — Npgsql rejects non-UTC values for `timestamptz`)

### Rules (enforced in `BookingService`; rules 3 and 4 also by the database)

1. **Date range:** from today up to and including today + 14 days (config `Booking:MaxDaysAhead`, default 14). No past dates.
2. **Weekdays only:** Saturday and Sunday cannot be booked. *(my call — the office is closed)*
3. **One booking per resource per day:** unique index `IX_Bookings_Resource_Date` on `(ResourceId, Date)`.
4. **One desk and one parking spot per person per day:** unique index `IX_Bookings_Person_Type_Date` on `(PersonId, ResourceType, Date)`.
5. **Active only:** inactive people and inactive resources cannot make or receive new bookings.
6. **Cancel:** only your own booking, only for today or a future date. Cancelling deletes the row.
7. **Deactivating a resource or person** hides it for new bookings; **existing bookings stay valid**. Nothing is ever hard-deleted from the admin screens. *(my call — avoids cancellation side-effects and notifications)*
8. **"Today"** is computed in `Europe/Brussels` (config `App:TimeZone`).

### One shared calendar *(my call, from review)*

All date logic lives in one place in `Booking.Core`, so the UI arrows, the booking rules and the email schedule always agree, and tests can freeze time:

```csharp
public interface IOfficeCalendar
{
    DateOnly Today { get; }                 // in App:TimeZone
    DateOnly FirstBookableDay { get; }      // today, or next weekday if today is a weekend
    DateOnly LastBookableDay { get; }       // today + MaxDaysAhead
    bool IsBookable(DateOnly date);         // weekday and within range
    DateOnly NextWeekday(DateOnly date);
    DateOnly PreviousWeekday(DateOnly date);
}
```

Implemented on top of .NET's `TimeProvider`; tests use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) so they pass on any day of the week.

### Data access and concurrency *(my call, from review)*

- Register EF Core with **`AddDbContextFactory`**; every service method creates and disposes its own `DbContext`. (In Blazor Server a scoped context lives for the whole browser session, which breaks under fast double clicks and keeps failed saves around.) The background email job uses the same factory.
- Two simultaneous requests for the same spot both pass the service checks; the database rejects the second. Catch `DbUpdateException` wrapping a `PostgresException` with `SqlState == "23505"` and map by `ConstraintName`: `IX_Bookings_Resource_Date` → `ResourceTaken`, `IX_Bookings_Person_Type_Date` → `AlreadyBookedThatType`. Never shown as a crash.

### The booking contract (written in the skeleton, implemented by Person A)

```csharp
public interface IBookingService
{
    Task<DayOverview> GetDayAsync(DateOnly date, CancellationToken ct = default);
    Task<BookingResult> BookAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default);
    Task<BookingResult> CancelAsync(int personId, int bookingId, CancellationToken ct = default);
}

public sealed record DayOverview(
    DateOnly Date,
    IReadOnlyList<ResourceSlot> Desks,
    IReadOnlyList<ResourceSlot> Parking,
    IReadOnlyList<string> PeopleInOffice);   // distinct names with any booking that day

public sealed record ResourceSlot(
    int ResourceId, string Code, string Name, ResourceType Type,
    int? BookingId, int? BookedByPersonId, string? BookedByName);
    // DayOverview lists active resources, plus inactive ones that still have a booking that day

public sealed record BookingResult(bool Success, BookingError? Error, int? BookingId);

public enum BookingError
{
    OutOfRange, Weekend, ResourceTaken, AlreadyBookedThatType,
    PersonInactive, ResourceInactive, NotFound, NotYourBooking
}
```

### The admin contract (written in the skeleton, implemented by Person C)

```csharp
public interface IAdminService
{
    Task<IReadOnlyList<Resource>> GetResourcesAsync(CancellationToken ct = default);
    Task<Resource> AddResourceAsync(string code, string name, ResourceType type, CancellationToken ct = default);
    Task UpdateResourceAsync(int id, string code, string name, bool isActive, CancellationToken ct = default);
    Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken ct = default);
    Task<Person> AddPersonAsync(string name, string? email, CancellationToken ct = default);
    Task UpdatePersonAsync(int id, string name, string? email, bool isActive, CancellationToken ct = default);
}

public sealed class AdminValidationException(string message) : Exception(message);
```

Invalid input (empty fields, duplicate resource `Code`) throws `AdminValidationException` with a readable message; admin pages catch it and show the message. *(my call — an unhandled exception in Blazor Server kills the user's session)*

### Current user (written in the skeleton, implemented by Person B)

```csharp
public interface ICurrentUser
{
    Task<int?> GetPersonIdAsync();
    Task SetAsync(int personId);
    event Action? Changed;    // header picker raises it; the day view re-renders
}
```

Registered as scoped (one per browser session).

The skeleton registers **stub implementations** of `IBookingService` and `IAdminService` (returning fixed fake data), so Persons B and C can build their screens immediately. Persons A and C swap in the real implementations.

## 6. Solution layout and ownership

```
Dotnetjes.slnx               (.NET 10's `dotnet new sln` creates .slnx)
.config/dotnet-tools.json     dotnet-ef as a local tool, so a clean clone can run migrations
src/
  Booking.Core/              ── Person A
    Entities/                 Person, Resource, Booking, ResourceType
    Data/                     BookingDbContext, Migrations/, SeedData
    Calendar/                 IOfficeCalendar, OfficeCalendar
    Services/                 IBookingService, BookingService, DTOs above
    Admin/                    IAdminService, AdminValidationException (interfaces only)
  Booking.Web/               ── Blazor Web App
    Program.cs                skeleton only — all DI registrations written up front
    Components/Layout/        header with "You: <name> ▾" picker      ── Person B
    Components/Pages/Day.razor   route "/"                           ── Person B
    CurrentUser/              ICurrentUser + implementation           ── Person B
    Components/Pages/Admin/   routes "/admin/resources", "/admin/people" ── Person C
    Admin/AdminService.cs     real IAdminService                      ── Person C
    Notifications/            DailySummaryService (BackgroundService), email sender ── Person C
tests/
  Booking.Core.Tests/        xUnit + Testcontainers.PostgreSql + FakeTimeProvider ── Person A
Dockerfile                    skeleton
docker-compose.yml            skeleton (db + mailpit + app)
```

**Rendering *(my call, from review)*:** the whole app is **Interactive Server with prerendering off** (set on `<Routes>` and `<HeadOutlet>` in `App.razor`). Otherwise the header picker in the layout renders as static HTML and does nothing, and browser storage can't be read during prerender.

**Collision rules:** only Person A changes entities, the DbContext and migrations — others ask A. Nobody edits `Program.cs` after the skeleton except to swap a stub registration for a real one. `Booking.Web` references `Microsoft.EntityFrameworkCore.Design` (needed for `dotnet ef`). Since EF Core 9, startup `Migrate()` throws if the model changed without a new migration — so any model change must come with its migration in the same commit.

## 7. Parts in detail

### Skeleton — built by Person A *(my call, from review)*
Person A owns the data model, so A builds the skeleton (in practice with Claude, in this session, right after the plan is approved). Meanwhile B and C read their plan sections and set up their machines (Docker running, repo cloned).

The skeleton contains: solution and projects; the full entity model with both named unique indexes; initial migration; seed data; `IOfficeCalendar` with its real implementation; the three interfaces with stubs; `Program.cs` with every registration; global Interactive Server rendering; empty Day and Admin pages; the Dockerfile; and `docker-compose.yml` with all three services. **Done when** a clean clone runs `docker compose up --build` and shows the empty day page. Pushed to `main`.

### Person A — booking rules
- On startup the app applies migrations and seeds sample data **if the database is empty**: desks `D1`–`D8`, parking `P1`–`P4`, six people with fake names and `@example.com` emails.
- `BookingService` implementing all rules in §5, built test-first. Tests run against real Postgres via Testcontainers with a frozen clock: one test per rule, plus a concurrent double-booking test.
- Swap the stub `IBookingService` registration for the real one.

### Person B — the day view
- Header: "You: Anna ▾" dropdown listing active people. The choice is stored in the browser's **plain `localStorage` via JS interop** — not `ProtectedLocalStorage`, whose encryption keys are lost when the container is recreated (demo step 6). *(my call, from review)* Until a person is chosen, Book buttons are disabled and the page prompts "Pick who you are".
- `/` opens on `IOfficeCalendar.FirstBookableDay`. ◀ ▶ step through weekdays using the calendar; arrows disable outside the bookable range.
- Two columns, Desks and Parking. Each row: code, name, and either *free* + **Book**, *taken by X*, or *You* + **Cancel**.
- Below: "In the office: …" list.
- Errors from `BookingResult` show inline in plain language (e.g. "P2 was just taken — pick another one").

```
 Fri 20 Nov      [◀] [▶]                 You: Anna ▾
 DESKS                      PARKING
 D1  Ben          ✕         P1  Ben         ✕
 D2  free     [Book]        P2  free    [Book]
 D3  You    [Cancel]        P3  You   [Cancel]
 In the office: Ben, Anna, Chris
```

### Person C — admin and daily email
- `/admin/resources` and `/admin/people`: list, add, edit name/code/email, toggle active, show `AdminValidationException` messages. Link to Admin in the nav.
- Real `AdminService`; swap the stub registration.
- `DailySummaryService` (BackgroundService): on weekdays at 07:00 in `App:TimeZone` (config `Summary:SendAt`), sends **a separate email to each active person with an email address**. Each email lists today's date, who's in, who has which desk and parking spot, and how many desks and spots are free. *(my call on recipients)*
- **"Send today's summary now"** button on the admin page, so the demo doesn't wait for 07:00.
- SMTP via MailKit; settings `Smtp:Host`, `Smtp:Port`, `Smtp:From`.
- Owns `Dockerfile` and `docker-compose.yml` from the skeleton onward (adding settings as needed).

### Docker details (built in the skeleton)
- **Dockerfile:** multi-stage (`mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`), app listens on 8080.
- **docker-compose.yml:**
  - `db`: **`postgres:18-alpine`** (team decision), data in the repo folder `.container-data/postgres` bind-mounted on `/var/lib/postgresql` (Postgres 18 keeps data in `/var/lib/postgresql/18/docker`; mounting `.../data` like older versions makes it refuse to start), healthcheck over TCP (`pg_isready -h 127.0.0.1`, so it is not fooled by the socket-only server during first-time init), port `5432:5432` published for local development.
  - `mailpit`: `axllent/mailpit`, UI on `localhost:8025`, SMTP on 1025.
  - `app`: builds the Dockerfile, `depends_on: db (service_healthy)`, port `8080:8080`, `ConnectionStrings__Booking` pointing at `db`, `Smtp__Host=mailpit`.
- Local development: `docker compose up db mailpit`, then `dotnet run` the web project with `appsettings.Development.json` pointing at `localhost`.

## 8. Team workflow and timeline

| Time | What |
|---|---|
| 0:00–0:45 | Design (this doc) and implementation plan |
| 0:45–1:05 | **Skeleton** (Person A + Claude). B and C read their plan sections and prepare their machines. Pushed to `main`. |
| 1:05–3:15 | **Parallel build.** Each person on a branch (`feat/booking-rules`, `feat/day-view`, `feat/admin-email`) executing their plan section with superpowers. **About 5–6 tasks per person** — subagent review per task takes time. **Checkpoint merge to `main` at ~2:15**, and run `docker compose up --build` after each merge. |
| 3:15–3:45 | Integration: merge everything, run §9 on a clean clone, fix. |
| 3:45–4:00 | Demo |

## 9. Demo script (acceptance test)

1. Clean clone → `docker compose up --build`. App on `http://localhost:8080`, Mailpit on `http://localhost:8025`.
2. Pick **Anna**; book desk **D2** and parking **P2** for **today**. (Today is a weekday; if the demo runs on a weekend, use the first bookable day.)
3. Switch to **Ben**: D2 and P2 show "taken by Anna". Ben books D3, then tries to book D4 the same day → refused with a clear message.
4. Admin: add desk **D9**, deactivate **P4** → the day view shows D9 and no longer offers P4.
5. Admin: **Send today's summary now** → each active person has an email in Mailpit listing Anna (D2, P2) and Ben (D3).
6. `docker compose down` then `up` → bookings are still there, and the browser still remembers who you are.

## 10. Left to the builder

- Blazor component structure, styling (the default template's Bootstrap is fine).
- Exact seed names, EF configuration details beyond §5, email HTML layout.
- Test project details beyond "one test per rule + concurrency test against real Postgres with a frozen clock".
- Stretch goals, only if a person is completely done: parking waitlist, "book my usual desk" shortcut, week view.
