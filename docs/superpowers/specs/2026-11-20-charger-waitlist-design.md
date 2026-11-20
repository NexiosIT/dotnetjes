# Charger spot waitlist — design

**Date:** 2026-11-20
**Status:** draft, waiting for approval (builder check done)

## Why

EV drivers need a parking spot with a charger. The charger spots fill up, and today the only way to get one that frees up later is to keep checking the Book page. A waitlist lets them hear about it by email instead.

## Goals

- When every charger spot on a day is taken, a person can join that day's **charger waitlist**.
- When a charger spot on that day loses its booking, everyone on that day's waitlist gets an **email**: the first to book it gets it (no reservation, no automatic booking).
- A person who already has a parking spot that day can move to the freed charger spot in **one click** ("Switch here"), without first cancelling and risking losing both.
- Cancelling or switching stays instant: the emails go out in the background.

## Non-goals

- A waitlist for ordinary parking spots or desks.
- Automatically booking or holding a spot for the first in line.
- Emails when an admin adds a charger spot, reactivates one, or ticks "has charger" on an existing spot. The spot just shows as free on the Book page. *(my call: rare, keeps this small)*
- Guaranteed delivery: if the app restarts in the seconds between a cancellation and the email being sent, that email is lost. The free spot still shows in the app. *(agreed: approach 2 below)*
- Showing the waitlist to admins.
- A "pick the date from the email link" deep link. *(my call; see "Email")*

## Anti-goals (failure even if it "works")

- A Cancel or Switch click that hangs or errors because the mail server is slow or down.
- A Switch that leaves the person with **no** spot (old one released, new one already taken).
- Two people both ending up booked on the same spot (the existing unique indexes must keep deciding races).
- Emailing someone about a spot they themselves just freed.

## Constraints

- Same stack as the rest of the app: .NET 10, Blazor Interactive Server, EF Core + Npgsql, xUnit + Testcontainers for Core tests.
- Booking rules live in `Booking.Core`, not in the UI (original design). The waitlist rules too.
- Data access only through `IDbContextFactory<BookingDbContext>`, one context per service method.
- Email goes through the existing `IEmailSender` / `SmtpEmailSender` (one SMTP connection per email; a failing address is logged and never stops the others, like `DailySummarySender`).
- "Today" and bookable days come from `IOfficeCalendar` (weekdays, today … today + `Booking:MaxDaysAhead`).
- A "charger spot" is a `Resource` with `Type = Parking`, `HasCharger = true`, `IsActive = true`.

## Approaches (how the email gets sent)

1. **Send in the cancel click.** Least code, but a slow or broken mail server makes Cancel hang or fail. Rejected (anti-goal).
2. **In-app background queue — chosen.** Cancel/Switch puts a "charger spot freed" message on an in-memory queue and returns; a background service (like `DailySummaryService`) sends the emails. Instant for the user, little code. Cost: a message still in the queue is lost on restart; acceptable for a heads-up email.
3. **Database outbox + background sender.** Nothing lost, but an extra table, retries and cleanup for a convenience email. Rejected as too much for what it buys.

## Design

### Waitlist data

New entity `WaitlistEntry`: `Id`, `PersonId`, `Date` (DateOnly), `CreatedAt` (UTC). Unique index on `(PersonId, Date)` — one entry per person per day. A migration adds the table. The waitlist is only for charger spots, so there is no "type" column.

### Joining and leaving (new `IWaitlistService` in `Booking.Core`)

- **Join(personId, date)** succeeds only when all of these hold, else a specific error (new `WaitlistError` enum):
  - the date is bookable (weekday, within range) — same checks and order as `BookAsync`;
  - the person is active;
  - the person has an email address;
  - no active charger spot is free that day;
  - the person doesn't already have a charger spot booked that day;
  - joining twice is not an error: already on the list → success, nothing changes.
- **Leave(personId, date)**: removes the entry; not on the list → success.
- **IsOnWaitlist(personId, date)** for the Book page.
- **Leaving automatically:** when a person books (or switches to) a charger spot for a day, their waitlist entry for that day is removed, in the same database transaction as the booking.
- Entries for past days are simply ignored by every query. Deleting old rows is left to the builder (optional).

### When a charger spot frees up

A charger spot "frees up" when its booking for a day is removed by **CancelAsync** or by someone **switching away** from it, and the spot is still active with a charger (an inactive spot frees nothing). In both cases, after the change is saved, Core calls a new `IWaitlistNotifier.ChargerSpotFreed(date, resourceId, freedByPersonId)`. This call must not block or throw into the booking call.

The Web project implements it with an in-memory queue (`System.Threading.Channels`) read by a `BackgroundService`. For each message, the service (in a fresh scope) asks a Core sender class to email everyone who:
- has a waitlist entry for that date,
- is active and has an email address,
- is not the person who freed the spot,
- doesn't hold a charger booking that day by now.

Each email is sent separately; a failure is logged and the others continue. The spot may well be booked again by the time the email arrives — that's expected ("first to book gets it").

### Email

- Subject: `A charger spot is free on Tue 24 Nov` (date format like the Book page, `ddd d MMM`, invariant culture).
- Body (HTML, same simple style as the daily summary): which spot (code, name, location label), that whoever books first gets it, that they're on the waitlist for that day and will be emailed again if another one frees up, and a link to the app.
- The link uses a new setting `App:PublicUrl` (e.g. `https://booking.example.org`). Empty → the email has no link. Add it to `appsettings.json` (empty), `docker-compose.yml`/`.env.example` (`http://localhost:8080` locally). The link goes to the Book page; the person picks the day themselves. *(my call: the Book page has no date-in-URL today; adding one is a separate change)*

### Switch

New `IBookingService.SwitchAsync(personId, resourceId, date)`:
- Same validation as `BookAsync` for the target spot and day, except the "already have one of that type" check. Order: NotFound (person/spot) → OutOfRange → Weekend → PersonInactive → ResourceInactive → NotFound (no existing booking of that type) → ResourceTaken. Switching onto the spot you already hold is a no-op success. *(my calls)*
- The person must already have a booking of the same type (desk or parking) that day, else `NotFound`.
- In **one database transaction**: delete the old booking, insert the new one. If the insert loses a race (unique index → `ResourceTaken`), roll back: the person keeps the old booking and sees "{code} was just taken — pick another one."
- Switching onto a charger spot removes the person's waitlist entry for that day (same transaction).
- Switching away from a charger spot triggers `ChargerSpotFreed` for the old spot (after commit).
- Returns the new booking id.

### Book page (`Day.razor`, `SlotRow.razor`)

- On a free spot, the button reads **Switch here** instead of **Book** when the person already has a booking of that type that day; it calls `SwitchAsync`. Applies to desks and parking.
- Under the parking list, **only when the ⚡ Charger filter is on and no active charger spot is free that day**:
  - not on the list: `All charger spots are taken on Tue 24 Nov.  [Notify me when one frees up]`
  - on the list: `You're on the waitlist for a charger spot on Tue 24 Nov.  [Leave]`
  - no email address: the line says the waitlist needs an email address, which comes from their company (Keycloak) account, so they should ask whoever manages accounts; no button. *(emails come from Keycloak at login and can't be edited in the app)*
  - already holds a charger spot that day: no waitlist line at all. *(my call)*
- Waitlist errors show in the same message area as booking errors, in plain language (like `BookingMessages`).
- Day changes reload the waitlist state along with the day.

## Tests (what must be pinned)

- Join: each refusal reason; joining twice is fine; can't join while a charger spot is free; can join for several days.
- Booking/switching onto a charger spot removes that day's entry; booking an ordinary spot doesn't.
- Cancel and switch-away of a charger spot call the notifier with the right date/resource/person; cancelling an ordinary spot doesn't.
- Recipient selection: excludes the person who freed it, inactive people, people without email, people who already hold a charger spot that day, and entries for other days.
- One failing email doesn't stop the others.
- Switch: success moves the booking; target taken → old booking kept; no existing booking → `NotFound`; concurrent switches onto the same spot → exactly one wins and the loser keeps their old spot.

## Left to the builder

- Names of classes, the exact `WaitlistError` values and their messages (plain language, in the style of `BookingMessages`).
- Whether to delete waitlist entries for past days (and when).
- How the notifier queue is sized/bounded, as long as `ChargerSpotFreed` never blocks the booking call.
- `BookingService` gets the notifier as a dependency; existing tests that construct `BookingService` directly use a no-op notifier.
- Exact email HTML, in the style of the daily summary.
