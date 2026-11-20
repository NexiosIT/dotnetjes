# Charger Waitlist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** EV drivers can join a per-day waitlist when all charger spots are taken, get an email when one frees up, and move to it in one click ("Switch here").

**Architecture:** `Booking.Core` gets a `WaitlistEntry` table, an `IWaitlistService` (join/leave/check), a `SwitchAsync` on `IBookingService`, and an `IWaitlistNotifier` port that `BookingService` calls after a charger spot loses its booking. `Booking.Web` implements the notifier as an in-memory `Channel` drained by a `BackgroundService` that calls Core's `WaitlistEmailSender`. The Book page shows the waitlist line and "Switch here".

**Tech Stack:** .NET 10, Blazor Interactive Server, EF Core 10.0.15 + Npgsql, `System.Threading.Channels`, xUnit v2 + Testcontainers (Core tests need Docker).

**Spec:** `docs/superpowers/specs/2026-11-20-charger-waitlist-design.md`. Read it first.

## Global Constraints

- A **charger spot** = `Resource` with `Type == Parking && HasCharger && IsActive`.
- Booking and waitlist rules live in `Booking.Core`; data access only via `IDbContextFactory<BookingDbContext>`, one context per method.
- `IWaitlistNotifier.ChargerSpotFreed` is called **after** the change is committed, **never blocks and never throws** into the booking call.
- Emails go through the existing `IEmailSender`; one failing address is logged and never stops the others.
- Email subject exactly: `A charger spot is free on {date:ddd d MMM}` (invariant culture), e.g. `A charger spot is free on Tue 24 Nov`.
- New setting `App:PublicUrl`; empty → email has no link.
- Switch: one transaction; target lost to a race → old booking kept, error `ResourceTaken`.
- Existing unique indexes `IX_Bookings_Resource_Date` and `IX_Bookings_Person_Type_Date` keep deciding races. New index name: **`IX_Waitlist_Person_Date`**.
- Seed data (used by Core tests): parking P1 (Basement, charger), P2 (Basement), P3 (Outside, charger), P4 (Behind the building). Demo people Anna, Ben, Chris, Dana, Eli, Fien with `<name>@example.com`. Test "today" is Wed 2026-11-18.

## Review Focus

1. **Two people click "Switch here" on the same freed charger spot at once** → exactly one gets it; the other keeps their old spot and sees "{code} was just taken". *(Task 3: `Concurrent_switches_onto_same_spot_one_wins_loser_keeps_old`)*
2. **Mail server down or slow while someone cancels a charger spot** → Cancel returns success immediately; nothing thrown. *(Task 5: `ChargerSpotFreed_never_blocks_or_throws_when_queue_is_full`; Task 4: `One_failing_address_does_not_stop_others`)*
3. **Waiting person books a charger spot through the normal Book button** (not Switch) → their entry for that day disappears, so they're not emailed again. *(Task 2: `Booking_a_charger_spot_removes_waitlist_entry`)*
4. **Same two-tab double click on "Notify me"** → one entry, both calls succeed. *(Task 1: `Concurrent_joins_create_one_entry`)*
5. **Charger spot freed today (same day)** → waiters for today are emailed. *(Task 4: `Emails_waiters_for_that_date_only` uses today's date)*

---

### Task 1: Waitlist table and `WaitlistService`

**Files:**
- Create: `src/Booking.Core/Entities/WaitlistEntry.cs`
- Create: `src/Booking.Core/Waitlist/IWaitlistService.cs` (interface + `WaitlistError` + `WaitlistResult`)
- Create: `src/Booking.Core/Waitlist/WaitlistService.cs`
- Modify: `src/Booking.Core/Data/BookingDbContext.cs` (DbSet + index)
- Create: migration `AddWaitlist` in `src/Booking.Core/Data/Migrations/`
- Modify: `src/Booking.Web/Setup/BookingFeature.cs` (register service)
- Test: `tests/Booking.Core.Tests/Waitlist/WaitlistServiceTests.cs`

**Interfaces:**
- Produces:
  - `public class WaitlistEntry { int Id; int PersonId; Person Person; DateOnly Date; DateTime CreatedAt; }`
  - `BookingDbContext.WaitlistIndex = "IX_Waitlist_Person_Date"`, `DbSet<WaitlistEntry> Waitlist`
  - `public enum WaitlistError { NotFound, OutOfRange, Weekend, PersonInactive, NoEmail, ChargerSpotFree, AlreadyHasCharger }`
  - `public sealed record WaitlistResult(bool Success, WaitlistError? Error)` with `Ok()` / `Fail(error)`
  - `public interface IWaitlistService { Task<WaitlistResult> JoinAsync(int personId, DateOnly date, CancellationToken ct = default); Task LeaveAsync(int personId, DateOnly date, CancellationToken ct = default); Task<bool> IsOnWaitlistAsync(int personId, DateOnly date, CancellationToken ct = default); Task<bool> HasEmailAsync(int personId, CancellationToken ct = default); }`
  - Constructor: `WaitlistService(IDbContextFactory<BookingDbContext>, IOfficeCalendar, TimeProvider)`

- [ ] **Step 1: Write the failing tests** (`[Collection("postgres")]`, reuse `BookingTestContext` for DB/people/resources; construct `WaitlistService(ctx.DbFactory, calendar, ctx.Time)` — add a `Calendar` property to `BookingTestContext` exposing the `OfficeCalendar` it builds)

```csharp
Join_succeeds_when_all_charger_spots_taken      // book P1 (Ben), P3 (Chris) for Today; Anna joins → Success, IsOnWaitlist true
Join_refused_while_a_charger_spot_is_free       // only P1 booked → ChargerSpotFree
Join_ignores_inactive_free_charger_spot         // P1 booked, P3 deactivated → Success
Join_refused_for_person_with_charger_spot       // Anna holds P1, P3 taken by Ben → AlreadyHasCharger
Join_refused_without_email                      // set Anna.Email = null → NoEmail
Join_refused_for_inactive_person                // → PersonInactive
Join_refused_on_weekend                         // Sat 2026-11-21 → Weekend
Join_refused_out_of_range                       // Today.AddDays(-1) and Today.AddDays(20) → OutOfRange
Join_unknown_person_is_NotFound
Join_twice_is_fine_one_entry                    // both Success; 1 row
Concurrent_joins_create_one_entry               // Task.WhenAll of two JoinAsync → both Success; 1 row
Can_join_several_days                           // Today and Today.AddDays(1) → 2 rows
Leave_removes_entry_and_is_fine_when_absent
HasEmail_reflects_person_email                  // Anna true; after Email = null or "  " → false
```

Check order for Join: NotFound → OutOfRange → Weekend → PersonInactive → NoEmail → AlreadyHasCharger → ChargerSpotFree.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Booking.Core.Tests --filter "FullyQualifiedName~WaitlistServiceTests"`
Expected: build FAIL (types missing).

- [ ] **Step 3: Entity, DbContext, migration**

Index `(PersonId, Date)` unique named `WaitlistIndex`; table name `Waitlist`.
Run: `dotnet ef migrations add AddWaitlist --project src/Booking.Core --output-dir Data/Migrations`
Expected: migration creating `Waitlist` with FK to `People` and `IX_Waitlist_Person_Date`.

- [ ] **Step 4: Implement `WaitlistService`**

A unique-violation on `IX_Waitlist_Person_Date` during Join is treated as success (concurrent double click). Range/weekend checks mirror `BookingService.BookAsync`. Register `services.AddScoped<IWaitlistService, WaitlistService>()` in `BookingFeature`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Booking.Core.Tests --filter "FullyQualifiedName~WaitlistServiceTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Booking.Core src/Booking.Web/Setup/BookingFeature.cs tests/Booking.Core.Tests
git commit -m "feat: charger waitlist - join, leave, check"
```

### Task 2: Notifier port; Book removes entry, Cancel notifies

**Files:**
- Create: `src/Booking.Core/Waitlist/IWaitlistNotifier.cs` (interface, `ChargerSpotFreed` record, `NoWaitlistNotifier`)
- Modify: `src/Booking.Core/Services/BookingService.cs`
- Modify: `tests/Booking.Core.Tests/Bookings/BookingTestContext.cs` (pass a `RecordingNotifier`, expose it)
- Create: `tests/Booking.Core.Tests/Waitlist/RecordingNotifier.cs`
- Test: `tests/Booking.Core.Tests/Waitlist/WaitlistBookingTests.cs`

**Interfaces:**
- Consumes: `WaitlistEntry`, `DbSet Waitlist` (Task 1).
- Produces:
  - `public sealed record ChargerSpotFreed(DateOnly Date, int ResourceId, int FreedByPersonId);`
  - `public interface IWaitlistNotifier { void ChargerSpotFreed(ChargerSpotFreed freed); }`
  - `public sealed class NoWaitlistNotifier : IWaitlistNotifier` (does nothing)
  - `BookingService(IDbContextFactory<BookingDbContext>, IOfficeCalendar, TimeProvider, IWaitlistNotifier)`
  - test helper `RecordingNotifier : IWaitlistNotifier { List<ChargerSpotFreed> Freed }`

- [ ] **Step 1: Write the failing tests**

```csharp
Booking_a_charger_spot_removes_waitlist_entry        // Anna on list for Today (insert row directly); Anna books P1 → no row for (Anna, Today)
Booking_an_ordinary_spot_keeps_waitlist_entry        // Anna books P2 → row still there
Booking_charger_spot_keeps_entries_for_other_days    // Anna on list Today and Today+1; books P1 Today → Today+1 row stays
Cancelling_charger_spot_notifies                     // Ben books P1, cancels → Freed == [new(Today, P1, Ben)]
Cancelling_ordinary_spot_or_desk_does_not_notify     // P2 and D1 → Freed empty
Failed_cancel_does_not_notify                        // NotYourBooking → Freed empty
Cancelling_inactive_charger_spot_does_not_notify     // book P1, deactivate P1, cancel → Freed empty
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Booking.Core.Tests --filter "FullyQualifiedName~WaitlistBookingTests"`
Expected: build FAIL (notifier types / constructor).

- [ ] **Step 3: Implement**

- `BookAsync`: when the resource is a charger spot, delete the person's waitlist row for that date and insert the booking in **one transaction** (`db.Database.BeginTransactionAsync`); unique-violation handling unchanged.
- `CancelAsync`: before deleting, load the booking's resource; after a successful delete (`deleted == 1`) of a charger spot, call `notifier.ChargerSpotFreed(new(booking.Date, booking.ResourceId, personId))`.
- Register `IWaitlistNotifier` as `NoWaitlistNotifier` singleton in `BookingFeature` for now (Task 5 replaces it).

- [ ] **Step 4: Run all Core tests**

Run: `dotnet test tests/Booking.Core.Tests`
Expected: PASS (existing booking tests included).

- [ ] **Step 5: Commit**

```bash
git add src/Booking.Core src/Booking.Web/Setup/BookingFeature.cs tests/Booking.Core.Tests
git commit -m "feat: booking a charger spot leaves the waitlist; cancelling one notifies"
```

### Task 3: `SwitchAsync`

**Files:**
- Modify: `src/Booking.Core/Services/IBookingService.cs`, `src/Booking.Core/Services/BookingService.cs`
- Modify: `tests/Booking.Core.Tests/Notifications/SummaryFakes.cs` (`FakeBookingService.SwitchAsync` throws `NotSupportedException`)
- Test: `tests/Booking.Core.Tests/Bookings/SwitchTests.cs`

**Interfaces:**
- Consumes: notifier + waitlist removal (Task 2).
- Produces: `Task<BookingResult> SwitchAsync(int personId, int resourceId, DateOnly date, CancellationToken ct = default);` on `IBookingService`. Returns `BookingResult.Ok(newBookingId)`; switching onto the spot you already hold → `Ok(existingId)`, nothing changes.

- [ ] **Step 1: Write the failing tests**

```csharp
Switch_moves_parking_booking                       // Anna P2 → switch P1: P1 Anna, P2 free, Success
Switch_moves_desk_booking                          // D1 → D2
Switch_onto_own_spot_is_noop
Switch_without_existing_booking_is_NotFound
Switch_to_taken_spot_keeps_old_booking             // Ben holds P1; Anna (P2) switches to P1 → ResourceTaken; Anna still on P2
Concurrent_switches_onto_same_spot_one_wins_loser_keeps_old
    // Anna P2, Ben P4; Task.WhenAll(switch Anna→P1, switch Ben→P1)
    // exactly one Success; the other ResourceTaken and still holds their original spot
Switch_validation_matches_book                     // weekend → Weekend; inactive target → ResourceInactive; inactive person → PersonInactive
Switch_onto_charger_removes_waitlist_entry         // Anna on list, switches P2→P1 → no row
Switch_away_from_charger_notifies                  // Anna P1 → P2: Freed == [new(Today, P1, Anna)]
Switch_between_ordinary_spots_does_not_notify
```

Check order: NotFound (person/spot) → OutOfRange → Weekend → PersonInactive → ResourceInactive → NotFound (no existing booking of that type) → ResourceTaken.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Booking.Core.Tests --filter "FullyQualifiedName~SwitchTests"`
Expected: build FAIL (`SwitchAsync` missing).

- [ ] **Step 3: Implement `SwitchAsync`**

In one transaction: delete old booking (same person, type, date), remove waitlist row if target is a charger spot, insert new booking, `SaveChangesAsync`, commit. On unique violation (`ConflictFrom`) roll back and return the error. Concurrency: the delete-then-insert inside a transaction relies on `IX_Bookings_Resource_Date` for the target; if the loser's transaction sees the winner's uncommitted row, Postgres blocks until it commits, then raises the unique violation — that's the intended path. After commit, if the old spot was a charger spot (active), call `ChargerSpotFreed(date, oldResourceId, personId)`.

- [ ] **Step 4: Run all Core tests**

Run: `dotnet test tests/Booking.Core.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Booking.Core tests/Booking.Core.Tests
git commit -m "feat: switch to another spot in one step"
```

### Task 4: Waitlist email (recipients + composer)

**Files:**
- Create: `src/Booking.Core/Waitlist/WaitlistEmailComposer.cs`
- Create: `src/Booking.Core/Waitlist/WaitlistEmailSender.cs`
- Test: `tests/Booking.Core.Tests/Waitlist/WaitlistEmailComposerTests.cs`, `tests/Booking.Core.Tests/Waitlist/WaitlistEmailSenderTests.cs`

**Interfaces:**
- Consumes: `ChargerSpotFreed` (Task 2), `WaitlistEntry` (Task 1), existing `IEmailSender`, `SummaryEmail`, `SummaryResult(int Sent, int Failed)`, test `RecordingEmailSender`.
- Produces:
  - `public sealed record WaitlistEmailSettings(string? PublicUrl);`
  - `public static class WaitlistEmailComposer { public static SummaryEmail Compose(Person recipient, DateOnly date, Resource spot, string? publicUrl); }`
  - `public sealed class WaitlistEmailSender(IDbContextFactory<BookingDbContext>, IEmailSender, WaitlistEmailSettings, ILogger<WaitlistEmailSender>) { Task<SummaryResult> SendAsync(ChargerSpotFreed freed, CancellationToken ct); }`

- [ ] **Step 1: Write the failing tests**

Composer (pure):
```csharp
Subject_names_the_day          // date 2026-11-24 → "A charger spot is free on Tue 24 Nov"
Body_names_spot_and_location   // contains "P1", "Parking 1", "Basement", recipient name; HTML-encoded
Body_has_link_when_url_set     // publicUrl "https://booking.example.org" → contains href="https://booking.example.org"
Body_has_no_link_when_url_empty // null and "" → no "href"
To_is_recipient_email
```
Sender (postgres; waitlist rows inserted directly):
```csharp
Emails_waiters_for_that_date_only     // Anna, Chris on list Today; Dana on list Today+1 → Anna, Chris emailed
Skips_person_who_freed_the_spot       // Ben on list too, FreedBy Ben → Ben not emailed
Skips_inactive_and_emailless_waiters
Skips_waiter_who_now_holds_a_charger_spot   // Chris on list but holds P3 → not emailed
One_failing_address_does_not_stop_others    // RecordingEmailSender(failFor: "anna@example.com") → Sent 1, Failed 1
Inactive_or_non_charger_spot_sends_nothing  // freed resource P2, or P1 deactivated → Sent 0
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Booking.Core.Tests --filter "FullyQualifiedName~WaitlistEmail"`
Expected: build FAIL.

- [ ] **Step 3: Implement composer and sender**

Body in the daily summary's style (`<p>` paragraphs, `WebUtility.HtmlEncode`): greeting; "{code} – {name} ({location label}) has just become free on {date}."; "Whoever books it first gets it."; "You're on the waitlist for that day and will get another email if another charger spot frees up."; optional `<a href="{publicUrl}">Open the booking app</a>`. Sender logs `Waitlist email for {Date}: {Sent} sent, {Failed} failed`.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Booking.Core.Tests --filter "FullyQualifiedName~WaitlistEmail"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Booking.Core tests/Booking.Core.Tests
git commit -m "feat: waitlist email to everyone waiting for a charger spot"
```

### Task 5: Background sending in the web app

**Files:**
- Create: `src/Booking.Web/Notifications/ChannelWaitlistNotifier.cs`
- Create: `src/Booking.Web/Notifications/WaitlistNotificationService.cs`
- Modify: `src/Booking.Web/Setup/BookingFeature.cs` (needs `IConfiguration`; update the call in `Program.cs`)
- Modify: `src/Booking.Web/appsettings.json` (`"App": { "PublicUrl": "" }`), `docker-compose.yml` (`App__PublicUrl: http://localhost:8080`), `.env.example` (comment for production)
- Test: `tests/Booking.Web.Tests/Notifications/ChannelWaitlistNotifierTests.cs`

**Interfaces:**
- Consumes: `IWaitlistNotifier`, `ChargerSpotFreed` (Task 2); `WaitlistEmailSender`, `WaitlistEmailSettings` (Task 4).
- Produces:
  - `public sealed class ChannelWaitlistNotifier(ILogger<ChannelWaitlistNotifier>) : IWaitlistNotifier` with `public ChannelReader<ChargerSpotFreed> Reader`. Bounded channel, capacity **1000**, `FullMode = DropWrite`; a dropped message is logged as a warning.
  - `WaitlistNotificationService(ChannelWaitlistNotifier, IServiceScopeFactory, ILogger<...>) : BackgroundService` — reads messages, per message creates a scope and calls `WaitlistEmailSender.SendAsync`; any exception is logged and the loop continues (pattern of `DailySummaryService`).
  - DI: `ChannelWaitlistNotifier` singleton, `IWaitlistNotifier` → same instance; `WaitlistEmailSender` scoped; `WaitlistEmailSettings(configuration["App:PublicUrl"])` singleton; hosted service.

- [ ] **Step 1: Write the failing tests**

```csharp
ChargerSpotFreed_is_readable_from_the_reader
ChargerSpotFreed_never_blocks_or_throws_when_queue_is_full   // write 1001 messages synchronously; returns; Reader.Count == 1000
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Booking.Web.Tests --filter "FullyQualifiedName~ChannelWaitlistNotifierTests"`
Expected: build FAIL.

- [ ] **Step 3: Implement notifier, service, wiring, config**

- [ ] **Step 4: Run everything**

Run: `dotnet build` then `dotnet test`
Expected: no new warnings; all PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Booking.Web tests/Booking.Web.Tests docker-compose.yml .env.example
git commit -m "feat: send waitlist emails in the background"
```

### Task 6: Book page — Switch here and the waitlist line

**Files:**
- Modify: `src/Booking.Web/Components/Pages/SlotRow.razor` (`[Parameter] bool CanSwitch`, `EventCallback<ResourceSlot> Switch`)
- Modify: `src/Booking.Web/Components/Pages/Day.razor`
- Create: `src/Booking.Web/Components/Pages/ChargerWaitlist.cs` (pure state logic)
- Create: `src/Booking.Web/Components/Pages/WaitlistMessages.cs`
- Test: `tests/Booking.Web.Tests/ChargerWaitlistTests.cs`, `tests/Booking.Web.Tests/WaitlistMessagesTests.cs`

**Interfaces:**
- Consumes: `IWaitlistService` (Task 1), `IBookingService.SwitchAsync` (Task 3).
- Produces:
  - `public enum ChargerWaitlistLine { None, CanJoin, OnList, NeedsEmail }`
  - `public static class ChargerWaitlist { public static ChargerWaitlistLine For(DayOverview day, int? personId, bool chargerFilterOn, bool onWaitlist, bool hasEmail); public static bool HasBookingOfType(DayOverview day, int? personId, ResourceType type); }`
  - `public static class WaitlistMessages { public static string For(WaitlistError error); }`

- [ ] **Step 1: Write the failing tests**

```csharp
// ChargerWaitlist.For
None_when_filter_off
None_when_an_active_charger_spot_is_free
None_when_person_holds_a_charger_spot
CanJoin_when_all_taken_and_not_on_list
OnList_when_on_list
NeedsEmail_when_no_email
Inactive_free_charger_spot_counts_as_taken      // DayOverview can't tell inactive: see note below
// ChargerWaitlist.HasBookingOfType
True_when_person_has_parking_that_day / False_for_other_type / False_for_null_person
// WaitlistMessages
Every_error_has_a_message                       // Enum.GetValues<WaitlistError>() → non-empty, no throw
NoEmail_points_to_company_account               // contains "company account"
```

Note: `ResourceSlot` has no `IsActive`. Add `bool IsActive = true` as the last optional parameter of `ResourceSlot` and fill it in `GetDayAsync`, so "free charger spot" means `IsActive && HasCharger && BookingId is null`. Add a Core test `GetDay_marks_inactive_slots` in `tests/Booking.Core.Tests/Bookings/GetDayTests.cs`.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Booking.Web.Tests --filter "FullyQualifiedName~ChargerWaitlistTests|FullyQualifiedName~WaitlistMessagesTests"`
Expected: build FAIL.

- [ ] **Step 3: Implement**

- `ChargerWaitlist`, `WaitlistMessages` (plain language; `NoEmail`: "The waitlist emails you, but your company account has no email address. Ask whoever manages accounts to add one."; `ChargerSpotFree`: "A charger spot is free — book it now."; others in `BookingMessages` style).
- `SlotRow`: free spot shows **Switch here** (calls `Switch`) when `CanSwitch`, else **Book**.
- `Day.razor`: inject `IWaitlistService`; load `_onWaitlist` (`IsOnWaitlistAsync`) and `_hasEmail` (`HasEmailAsync`, Task 1) together with the day; pass `CanSwitch = ChargerWaitlist.HasBookingOfType(_day, _personId, slot.Type)`; `Switch` runs `Bookings.SwitchAsync` through the existing `Run`. Under the parking list render the line per `ChargerWaitlist.For(...)`:
  - `CanJoin`: `All charger spots are taken on {ddd d MMM}.` + button **Notify me when one frees up** → `JoinAsync`, show `WaitlistMessages.For` on error
  - `OnList`: `You're on the waitlist for a charger spot on {ddd d MMM}.` + button **Leave**
  - `NeedsEmail`: the `NoEmail` message, no button

- [ ] **Step 4: Run all tests**

Run: `dotnet build` then `dotnet test`
Expected: no new warnings; all PASS.

- [ ] **Step 5: Manual check** (`docker compose up --build`, Mailpit UI at the port in `docker-compose.yml`)
- Two users: Ben books P1, Chris books P3. Anna (with P2) turns on ⚡ Charger → sees "All charger spots are taken…" → **Notify me** → sees "You're on the waitlist…".
- Ben cancels P1 → Cancel is instant; Mailpit shows one email to Anna, subject `A charger spot is free on …`, none to Ben.
- Anna clicks **Switch here** on P1 → she holds P1, P2 is free, waitlist line gone.

- [ ] **Step 6: Commit**

```bash
git add src/Booking.Core src/Booking.Web tests
git commit -m "feat: Book page - switch spots and join the charger waitlist"
```
