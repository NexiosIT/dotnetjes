# Admin pages for Keycloak admins only — design

**Date:** 2026-11-20
**Status:** draft, waiting for approval
**Builds on:** [Keycloak login](2026-11-20-keycloak-login-design.md) ("Admin access (now and later)")

## Why

Today every logged-in user can open the Admin pages (Desks & parking, People, "Send today's summary now"). Only people the organisation marks as admin in Keycloak should.

## Goals

- Only users with the Keycloak role `Admin` can open `/admin/resources` and `/admin/people`, whether by menu, by link inside the app, or by typing the URL.
- The **Admin** menu link is shown only to admins.
- A non-admin who reaches an admin URL sees a plain "You don't have access to the admin pages" page, not "not found".
- Who is admin is decided in Keycloak only.

## Non-goals

- Any admin flag in the app's database.
- Role changes taking effect mid-session. Roles are read at login; a granted or removed role applies on the next login. *(my call: acceptable for an admin role; the cookie is the session)*
- Other roles or finer-grained permissions.

## Anti-goals

- An admin page reachable by a non-admin through in-app navigation (Blazor client-side routing skips the HTTP authorization check unless the router enforces it).
- Needing a Keycloak mapper change to make it work.

## Approaches

1. **Roles read from the access token at login + ASP.NET's built-in role/policy checks — chosen.** No new dependency, works with Keycloak's default token settings, about 30 lines.
2. Keycloak.AuthServices library — rejected for the same reason as in the login design: a single-maintainer dependency for little saved work.
3. An "is admin" flag on people in our database — rejected: the role would no longer come from Keycloak and could drift.

## Design

**Where the role lives.** A *client role* named `Admin` on the booking app's Keycloak client (`Keycloak:ClientId`). Keycloak puts client roles in the access token as `resource_access.<clientId>.roles` (a JSON claim). By default they are not in the ID token, which is what the app reads claims from today. *(my call: client role rather than realm role, so being admin elsewhere in the realm doesn't make you admin here)*

**At login.** In the OIDC `OnTokenValidated` event, read the access token (`context.TokenEndpointResponse.AccessToken`), parse `resource_access.<clientId>.roles`, and add one `role` claim per role to the user's identity. Set `TokenValidationParameters.RoleClaimType = "role"`. The parsing lives in a small static helper in `Auth/` (next to `KeycloakClaims`) so it is unit-testable without a Keycloak. Missing or malformed `resource_access` → no roles, never an exception (the user still logs in, just not as admin).

**Policy.** `AddAuthorization` gets a named policy `"Admin"` = authenticated + `RequireRole("Admin")`. The role and policy names are constants in one place.

**Pages.** `@attribute [Authorize(Policy = "Admin")]` on `Admin/Resources.razor` and `Admin/People.razor`. This covers the first HTTP request.

**Router.** `Routes.razor` switches from `RouteView` to `AuthorizeRouteView`, so in-app navigation to an admin page is also checked. Its `NotAuthorized` content shows the same "no access" message.

**No-access page.** A new page `/access-denied` with the message and a link back to booking. The cookie handler's `AccessDeniedPath` points at it (today it defaults to `/Account/AccessDenied`, which doesn't exist).

**Menu.** The Admin link in `NavMenu.razor` is wrapped in `<AuthorizeView Policy="Admin">`.

**Keycloak setup (for the README).** On the booking client: create client role `Admin`, assign it to the admins (directly or via a group). No mapper changes.

## Tests

- Role parsing: client role present → `Admin` role claim; role only on another client → none; no `resource_access` / malformed JSON → none.
- `AuthFeature`: the `"Admin"` policy exists and requires the role; `RoleClaimType` is `role`; `AccessDeniedPath` is `/access-denied`.
- Manual: admin sees the link and pages; non-admin doesn't see the link, gets the no-access page by URL and by in-app navigation.

## Left to the builder

- Exact wording and layout of the no-access page (keep it in the app's existing style).
- Whether the no-access content is one shared component used by both the page and the router.
