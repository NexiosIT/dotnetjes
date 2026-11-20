# Keycloak login — design

**Date:** 2026-11-20
**Status:** draft, waiting for approval

## Why

The organisation requires it: who may use the booking app is decided in the organisation's Keycloak, not in the app. Today there is no login at all. Anyone who opens the site picks a name from "Pick who you are" (stored in the browser's localStorage) and can book as anyone.

## Goals

- Every page requires a logged-in user. Not logged in → redirected to the organisation's Keycloak login page, then back to the app.
- The current person comes from the login. You always book and cancel as yourself.
- People come from Keycloak: a person is created the first time they log in (just-in-time). Their name and email are refreshed on every login.
- Works when deployed behind HTTPS (a reverse proxy in front of the container).
- The header shows the logged-in name and a **Log out** button. Logging out ends the Keycloak session too.
- Switching the Admin pages to a Keycloak role later must be a small change.

## Non-goals

- Copying all Keycloak users into the app in the background (option B in the discussion). People who never logged in don't exist in the app.
- An admin role check. **For now every logged-in user can open the Admin pages.** Later this becomes a Keycloak role.
- Booking on behalf of someone else.
- A local Keycloak in `docker compose`. Development uses the organisation's real Keycloak.
- Keeping existing people or bookings. The app isn't in use yet; the only people in the database are demo data. No linking of logins to existing people by email.

## Anti-goals (would count as failure even if login "works")

- A secret (client secret) committed to the repo.
- Two people records for the same Keycloak user (e.g. after they change their email in Keycloak).
- Someone getting into a page or a Blazor circuit without being logged in.

## Constraints

- .NET 10, Blazor Web App, whole app `InteractiveServerRenderMode(prerender: false)`; Postgres via `IDbContextFactory<BookingDbContext>`.
- Keycloak is already deployed by the organisation. Connection details and the client secret come later from the user. The client must allow `http://localhost:8080/signin-oidc` (and the logout return URL) for development.
- Runs in the chiseled Docker image (no shell, non-root). Data-protection keys are already persisted in `/keys`, so login cookies survive container rebuilds.
- `ICurrentUser` (`src/Booking.Web/CurrentUser/ICurrentUser.cs`) is the only way pages learn who the current person is. It was written so a real login could replace it without touching pages.

## Approaches considered

1. **ASP.NET Core's built-in OpenID Connect + cookie auth — chosen.** Microsoft's `Microsoft.AspNetCore.Authentication.OpenIdConnect` talks to Keycloak directly with the client ID and secret. A cookie keeps the user signed in. One fallback policy makes every page require login. Only one Microsoft package added. A role check later is a few lines.
2. **Keycloak.AuthServices library.** A wrapper around option 1 with Keycloak conveniences, mainly role-claim mapping. It saves a little work when the admin role arrives, but adds a dependency maintained by one community developer, and doesn't help today.
3. **Login gateway in front of the app (oauth2-proxy).** The app stays unaware of Keycloak, but you get an extra container, trusted identity headers, and a poor fit with Blazor's live connection.

The user picked option 1.

## Design

### Configuration

New section `Keycloak` (bound to an options class):

| Key | Example | Notes |
|---|---|---|
| `Keycloak:Authority` | `https://sso.example.org/realms/nexios` | Realm URL; the OIDC metadata is discovered from it |
| `Keycloak:ClientId` | `dotnetjes-booking` | Confidential client |
| `Keycloak:ClientSecret` | — | **Never in appsettings or git.** `dotnet user-secrets` for `dotnet run`; a `.env` file (already gitignored) feeding `docker-compose.yml` for Docker |

`appsettings.json` gets the section with `Authority` and `ClientId` empty. The app fails fast at startup with a clear message when any of the three is missing.

### Login flow

- Cookie authentication as the default scheme, OpenID Connect as the challenge scheme.
- Authorization-code flow with the client secret; scopes `openid profile email`; `SaveTokens = true` (the ID token is needed as `id_token_hint` for logout).
- **`ResponseMode = query`**, not the default `form_post`. With `form_post` the correlation and nonce cookies must be `SameSite=None`, which browsers reject without `Secure`, and development runs on plain `http://localhost:8080`. *(builder's call, recorded here because it's a known trap)*
- A fallback authorization policy requiring an authenticated user covers every endpoint, including the Blazor hub. Static assets (`MapStaticAssets`) stay anonymous so the login redirect isn't the only thing that loads.
- `AddCascadingAuthenticationState()` so components can read the user.
- Logout: a `POST /logout` endpoint (antiforgery-protected form in the header) signs out of the cookie and the OIDC scheme. That redirects to Keycloak's end-session endpoint, which returns to `/`, and `/` immediately asks for a fresh login.

### Linking the login to a person

`Person` gets one new column:

- `ExternalId` (`string?`, max 255, unique index `IX_People_ExternalId`): the Keycloak `sub` claim. It's stable even when the user's email or name changes.

A migration adds it. *(The original plan said only Person A changes entities; that rule was for the parallel build day and no longer applies.)*

A new Core service, `IPersonDirectory.SignInAsync(string externalId, string name, string? email)` → `Person`, runs once per login (from the OIDC `OnTokenValidated` event):

1. Find the person by `ExternalId`. Found → update `Name` and `Email` from Keycloak, done.
2. Else create a new active person with those values.

People are matched **only** by `ExternalId`, never by email: the app has no real data to keep, and email matching would let anyone whose Keycloak email matches take over another person's record. Two logins racing to create the same person hit the unique index; the loser re-reads and uses the existing row.

The person's id is added to the cookie as a claim (`booking:person_id`), so later requests don't hit the database to find out who you are.

Name: Keycloak's `name` claim, falling back to `preferred_username`. Email: the `email` claim (it may be missing; then email stays `null`).

**Deactivated people** still log in. `BookingService` already refuses their bookings with `PersonInactive` → "Your profile is inactive — ask an admin." The day view keeps working read-only for them. *(my call: blocking login entirely would hide the explanation)*

### Current user

- `LocalStorageCurrentUser` is replaced by `ClaimsCurrentUser`, which reads `booking:person_id` from the `AuthenticationStateProvider`.
- `ICurrentUser` loses `SetAsync`, `ClearAsync` and the `Changed` event: you can no longer switch person inside the app. `GetPersonIdAsync()` stays, so `Day.razor` keeps working unchanged apart from removing its `Changed` subscription.
- `UserPicker.razor` is removed. A new `UserMenu.razor` in the header shows the person's name and the **Log out** button.
- The "Pick who you are to book." message on the day view goes away: there's always a person now.

### People admin page

Names and emails now come from Keycloak, so:

- the **Add** form is removed,
- name and email are no longer editable,
- the **Active** checkbox stays: deactivating someone still stops them booking.

`IAdminService.AddPersonAsync` and the name/email part of `UpdatePersonAsync` are no longer used by the UI. The builder removes or narrows them (see "Left to the builder").

### Admin access (now and later)

Now: no extra check. The fallback policy already requires login for the Admin pages.

Later: add a named policy `"Admin"` that requires a Keycloak role (Keycloak puts realm roles in `realm_access.roles` and client roles in `resource_access.<client>.roles`; they need mapping to role claims), put `@attribute [Authorize(Policy = "Admin")]` on the Admin pages, and hide the menu link with `<AuthorizeView Policy="Admin">`. Nothing in this design should make that harder.

### Seed data

`SeedData` currently seeds six fake people (Anna, Ben, …, `@example.com`) when the database has no people. With real login they would be clutter that nobody can log in as. **Stop seeding people. Keep seeding desks and parking spots, and change the "already seeded" check to look at resources.** *(my call)* Demo people already in a developer's database stay until they reset it (`docker compose down -v` plus clearing `.container-data/`); harmless, since nobody can log in as them.

### Behind HTTPS

In production the container (plain HTTP on 8080) sits behind a reverse proxy that terminates HTTPS. Without help the app thinks it's on `http://` and sends Keycloak an `http://…/signin-oidc` return address, which Keycloak rejects, and the login cookies wouldn't be marked `Secure`.

Fix: the deployment sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. This is built into ASP.NET Core: it turns on forwarded-headers handling for `X-Forwarded-For` and `X-Forwarded-Proto`, and trusts them from any proxy. That's fine as long as the container is only reachable through the proxy. No code change; the local `docker-compose.yml` leaves it off. The production Keycloak client must allow `https://<prod-host>/signin-oidc` and the logout return URL. *(builder's call: if the proxy setup turns out to need more, e.g. a path prefix, that's a follow-up)*

### Docker

`docker-compose.yml` passes `Keycloak__Authority`, `Keycloak__ClientId` and `Keycloak__ClientSecret` from a `.env` file next to it. Add an `.env.example` with the three keys and empty values, so teammates know what to fill in.

## Testing

- `IPersonDirectory` gets Postgres-backed tests in `Booking.Core.Tests` (same `[Collection("postgres")]` fixture as the other services): first login creates an active person; second login with the same external id returns the same person and refreshes name and email; a different external id with the same email creates a separate person.
- `SeedData` test: no people seeded, resources still seeded once.
- Login itself is verified by hand against the real Keycloak once the user provides credentials: logged out → redirected to Keycloak → back on the day view with your name in the header; booking shows "You"; Log out → back at Keycloak login. Logging in a second time doesn't create a second person.

## Left to the builder

- Exact package version of `Microsoft.AspNetCore.Authentication.OpenIdConnect`: match the ASP.NET Core shared framework the app runs on.
- Whether `AddPersonAsync` is deleted or kept unused, and how `UpdatePersonAsync` is narrowed to just the active flag.
- Where the auth setup lives: a new `Setup/AuthFeature.cs`, wired in `Program.cs` (the "don't edit `Program.cs`" rule was for the build day).
- What happens when a Blazor circuit outlives the login cookie: acceptable to leave as is (the next full page load asks for login again).

## Decided with the user

- Login: option 1, ASP.NET Core's built-in OpenID Connect.
- People: created on first login (just-in-time), not synced in the background.
- Admin pages: open to every logged-in user for now; a Keycloak role later.
- Development: against the organisation's real Keycloak, no local one.
- No real data yet: no linking to existing people by email.
- Production: behind HTTPS.
