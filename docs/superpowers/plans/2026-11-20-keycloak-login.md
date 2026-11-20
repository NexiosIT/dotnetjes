# Keycloak Login Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every page requires a login through the organisation's Keycloak; the current person comes from that login and is created on first visit.

**Architecture:** ASP.NET Core cookie auth + the built-in OpenID Connect handler (authorization-code flow, client secret) pointed at Keycloak. A fallback policy makes every endpoint require login. A Core service `PersonDirectory` finds or creates the `Person` for a Keycloak user (`sub` → `Person.ExternalId`). `ICurrentUser` is re-implemented on top of the login instead of localStorage, so pages keep asking it who you are.

**Tech Stack:** .NET 10, Blazor Web App (Interactive Server), `Microsoft.AspNetCore.Authentication.OpenIdConnect`, EF Core 10 + Npgsql, xUnit v2, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-11-20-keycloak-login-design.md`. Read it first; the plan argues from it.

**Plan call that refines the spec:** the spec puts the person id in the login cookie as a `booking:person_id` claim, set once at login. The plan instead resolves the person from the `sub` claim **once per Blazor circuit** (`ClaimsCurrentUser` calls `PersonDirectory.SignInAsync`). Same behaviour for users, one cheap query per page load, and it fixes a hole the claim had: after a database reset (`docker compose down -v`) a cookie would still carry a person id that no longer exists. Now the person is simply re-created. Name and email are refreshed on every visit instead of every login. No `OnTokenValidated` hook needed.

## Global Constraints

- Target `net10.0`. New package: `Microsoft.AspNetCore.Authentication.OpenIdConnect` **10.0.15**, matching EF Core 10.0.15 and the 10.0.15 shared framework on this machine.
- Config section `Keycloak` with `Authority`, `ClientId`, `ClientSecret`. **The secret is never committed:** `dotnet user-secrets` (project `src/Booking.Web`) for `dotnet run`; `.env` (already in `.gitignore`) for Docker.
- Startup fails fast when any of the three is blank, always with this exact sentence: `Keycloak:Authority, Keycloak:ClientId and Keycloak:ClientSecret must be set.`
- OIDC: authorization-code flow, scopes `openid profile email`, `SaveTokens = true`, **`ResponseMode = "query"`** (form_post breaks on plain-HTTP localhost; see spec).
- Keycloak claims: user id = `sub`; name = `name`, else `preferred_username`; email = `email` (may be missing → `null`). Turn off inbound claim-type mapping (`MapInboundClaims = false`) so these names arrive as-is.
- `Person.ExternalId`: `string?`, unique index named **`IX_People_ExternalId`**. People are matched **only** by `ExternalId`, never by email.
- Redirect URIs to register on the Keycloak client: `http://localhost:8080/signin-oidc` (Docker), `http://localhost:5017/signin-oidc` (`dotnet run`), `https://<prod-host>/signin-oidc`; post-logout redirect: the site root of each.
- Production runs behind an HTTPS reverse proxy with `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. No code for this; local compose leaves it off.
- Every logged-in user may open the Admin pages. No role check.
- Data access only through `IDbContextFactory<BookingDbContext>`, one context per service method.
- No hard deletes from admin screens; deactivate instead.

## Review Focus

1. **Keycloak user with no `email` or no `name`** (common for admin-created users) → person still created; name from `preferred_username`, email `null`; no crash. *(pinned in Task 4: `KeycloakClaimsTests`)*
2. **Database reset while someone is still logged in** (`docker compose down -v`) → next page load re-creates their person; no "not found" errors. *(follows from `ClaimsCurrentUser` always going through `SignInAsync`, pinned by `ClaimsCurrentUserTests.Returns_person_id_from_directory` + `PersonDirectoryTests.SignIn_first_time_creates_active_person`; checked end-to-end in Task 6 Step 5)*
3. **Two tabs/devices logging in for the first time at the same moment** → exactly one person row. *(pinned in Task 1: `SignIn_concurrent_first_logins_create_one_person`)*
4. **Deactivated person logs in** → sees the day view; Book shows "Your profile is inactive — ask an admin."; logging in again does **not** re-activate them. *(pinned in Task 1: `SignIn_keeps_deactivated_person_inactive`; UI checked in Task 6)*
5. **Keycloak settings missing or half-filled** → app refuses to start with the one clear sentence above, not a cryptic OIDC error on first request. *(pinned in Task 4: `KeycloakOptionsTests`)*

---

### Task 1: `Person.ExternalId` and `PersonDirectory`

**Files:**
- Modify: `src/Booking.Core/Entities/Person.cs`, `src/Booking.Core/Data/BookingDbContext.cs`
- Create: migration `AddPersonExternalId` in `src/Booking.Core/Data/Migrations/`
- Create: `src/Booking.Core/People/IPersonDirectory.cs`, `src/Booking.Core/People/PersonDirectory.cs`
- Test: `tests/Booking.Core.Tests/People/PersonDirectoryTests.cs`, `tests/Booking.Core.Tests/Data/DatabaseTests.cs`

**Interfaces:**
- Produces: `Person.ExternalId` (`string?`); `BookingDbContext.PersonExternalIdIndex = "IX_People_ExternalId"`.
- Produces: `namespace Booking.Core.People; public interface IPersonDirectory { Task<Person> SignInAsync(string externalId, string name, string? email, CancellationToken ct = default); }`
- Produces: `public sealed class PersonDirectory(IDbContextFactory<BookingDbContext> dbFactory) : IPersonDirectory`

- [ ] **Step 1: Write the failing tests** in `PersonDirectoryTests` (`[Collection("postgres")]`, a fresh `postgres.CreateDatabaseAsync()` per test, no seeding needed):

```csharp
SignIn_first_time_creates_active_person()
    // SignInAsync("sub-1", "Anna Peeters", "anna@org.be") → Id > 0, Name "Anna Peeters", Email "anna@org.be", IsActive true, ExternalId "sub-1"
SignIn_again_returns_same_person_and_refreshes_details()
    // first ("sub-1","Anna","anna@old.be"), then ("sub-1","Anna Peeters","anna@new.be") → same Id; stored Name "Anna Peeters", Email "anna@new.be"; People.Count == 1
SignIn_same_email_different_sub_creates_separate_person()
    // ("sub-1","Anna","anna@org.be") and ("sub-2","Anna 2","anna@org.be") → different Ids; People.Count == 2
SignIn_keeps_deactivated_person_inactive()
    // sign in, set IsActive=false directly in the db, sign in again → returned person IsActive false
SignIn_concurrent_first_logins_create_one_person()
    // await Task.WhenAll(10 × SignInAsync("sub-1","Anna",null)) → all 10 results have the same Id; People.Count == 1
```

And in `DatabaseTests.Unique_indexes_exist_with_expected_names` add `Assert.Contains("IX_People_ExternalId", indexNames);`

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Booking.Core.Tests --filter "FullyQualifiedName~PersonDirectoryTests|FullyQualifiedName~Unique_indexes"`
Expected: build error, `PersonDirectory` / `ExternalId` don't exist.

- [ ] **Step 3: Add `ExternalId` and the index.** `Person.ExternalId` is `string?`, no length limit (the context's comment explains why columns have none). In `OnModelCreating`: `HasIndex(p => p.ExternalId).IsUnique().HasDatabaseName(PersonExternalIdIndex)`. Postgres allows many `NULL`s in a unique index, so existing people (no external id) are fine.

- [ ] **Step 4: Create the migration**

Run: `dotnet ef migrations add AddPersonExternalId --project src/Booking.Core --output-dir Data/Migrations`
Expected: a migration adding a nullable `ExternalId` text column and `IX_People_ExternalId`. (Design-time factory `BookingDbContextDesignFactory` is in Core. If `dotnet ef` isn't installed: `dotnet tool install --global dotnet-ef --version 10.0.15`.)

- [ ] **Step 5: Implement `PersonDirectory.SignInAsync`.** Trim `name`; blank email → `null`. Look up by `ExternalId`: found → update `Name`, `Email` (never `IsActive`), save, return. Not found → insert a new active person. If the insert throws `DbUpdateException` whose inner `PostgresException` is `SqlState == "23505"` with `ConstraintName == "IX_People_ExternalId"`, another request won the race: use a fresh context to re-read by `ExternalId` and return that row (refresh name/email on it too).

- [ ] **Step 6: Run the tests**

Run: `dotnet test tests/Booking.Core.Tests`
Expected: all pass (the existing 58 + 5 new + the index assertion).

- [ ] **Step 7: Commit**

```bash
git add src/Booking.Core tests/Booking.Core.Tests
git commit -m "feat: person directory keyed on Keycloak user id"
```

---

### Task 2: Stop seeding demo people

**Files:**
- Modify: `src/Booking.Core/Data/SeedData.cs`
- Create: `tests/Booking.Core.Tests/Infrastructure/DemoPeople.cs`
- Modify: `tests/Booking.Core.Tests/Data/DatabaseTests.cs`, `tests/Booking.Core.Tests/Admin/AdminServiceTests.cs:13-20`, `tests/Booking.Core.Tests/Bookings/BookingTestContext.cs:45`

**Interfaces:**
- Produces: `SeedData.EnsureSeededAsync(BookingDbContext db, CancellationToken ct = default)` seeds only D1–D8 and P1–P4, only when there are no resources.
- Produces (tests only): `internal static class DemoPeople { public static Task AddAsync(BookingDbContext db); }` adding Anna, Ben, Chris, Dana, Eli, Fien with `<lowercase name>@example.com` (exactly what `SeedData` adds today).

- [ ] **Step 1: Change the test first.** `DatabaseTests.Migrations_apply_and_seed_runs_once`: keep the 8 desks / 4 parking asserts, change the people assert to `Assert.Equal(0, await check.People.CountAsync());`.

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test tests/Booking.Core.Tests --filter Migrations_apply_and_seed_runs_once`
Expected: FAIL, expected 0 people, actual 6.

- [ ] **Step 3: Change `SeedData`**: remove the people, make the "already seeded" check `db.Resources.AnyAsync`, update the summary comment.

- [ ] **Step 4: Add `DemoPeople.AddAsync` and call it** right after `SeedData.EnsureSeededAsync(db)` in `AdminServiceTests.CreateAsync` and `BookingTestContext` (line 45). These tests use the names (`"Dana"`, `"Anna"`, …), so they need the people.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add src/Booking.Core/Data/SeedData.cs tests/Booking.Core.Tests
git commit -m "feat: stop seeding demo people; they come from Keycloak now"
```

---

### Task 3: People admin becomes read-only (except Active)

**Files:**
- Modify: `src/Booking.Core/Admin/IAdminService.cs`, `src/Booking.Core/Admin/AdminService.cs`
- Modify: `src/Booking.Web/Components/Pages/Admin/People.razor`
- Modify: `tests/Booking.Core.Tests/Admin/AdminServiceTests.cs`, `tests/Booking.Core.Tests/Notifications/SummaryFakes.cs:54-55`

**Interfaces:**
- Removes: `IAdminService.AddPersonAsync`, `IAdminService.UpdatePersonAsync`.
- Produces: `Task SetPersonActiveAsync(int id, bool isActive, CancellationToken ct = default)` on `IAdminService`; unknown id throws `AdminValidationException("That item no longer exists.")` (the existing `Gone()`).

- [ ] **Step 1: Update the tests.** In `AdminServiceTests`: delete `AddPerson_validates_email` and `UpdatePerson_accepts_null_email`; in `Update_unknown_id_fails` replace the person call with `service.SetPersonActiveAsync(9999, true)` (same message). Add:

```csharp
SetPersonActive_toggles_only_active()
    // Dana: SetPersonActiveAsync(dana.Id, false) → stored IsActive false, Name "Dana", Email "dana@example.com" unchanged
```

In `SummaryFakes.FakeAdminService` replace the two removed members with `SetPersonActiveAsync` throwing `NotSupportedException`.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Booking.Core.Tests --filter AdminServiceTests`
Expected: build error, `SetPersonActiveAsync` doesn't exist.

- [ ] **Step 3: Implement.** Replace the two methods in `IAdminService`/`AdminService` with `SetPersonActiveAsync`. Drop `NormalizeEmail` if nothing else uses it. Update the interface's doc comment (it mentions email rules).

- [ ] **Step 4: Update `People.razor`.** Remove the Add form, the edit row and Edit/Save/Cancel buttons and their fields. Keep the table (Name, Email, Active) and the Active checkbox, now calling `Admin.SetPersonActiveAsync(p.Id, !p.IsActive)` through the existing `RunAsync`. Under the heading add: `<p class="text-muted">People appear here after their first login. Names and emails come from Keycloak.</p>`

- [ ] **Step 5: Run the whole suite and build**

Run: `dotnet build Dotnetjes.slnx` then `dotnet test`
Expected: 0 warnings, all pass.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat: people admin shows Keycloak people; only Active stays editable"
```

---

### Task 4: Require Keycloak login

**Files:**
- Modify: `src/Booking.Web/Booking.Web.csproj` (package)
- Create: `src/Booking.Web/Auth/KeycloakOptions.cs`, `src/Booking.Web/Auth/KeycloakClaims.cs`, `src/Booking.Web/Setup/AuthFeature.cs`
- Modify: `src/Booking.Web/Program.cs`, `src/Booking.Web/appsettings.json`, `docker-compose.yml`
- Create: `.env.example`
- Test: `tests/Booking.Web.Tests/Auth/KeycloakOptionsTests.cs`, `tests/Booking.Web.Tests/Auth/KeycloakClaimsTests.cs`

**Interfaces:**
- Produces: `public sealed class KeycloakOptions { string Authority; string ClientId; string ClientSecret; bool IsComplete { get; } }` (all `= ""`; `IsComplete` true only when all three are non-blank), bound from section `Keycloak`.
- Produces: `public sealed record KeycloakUser(string ExternalId, string Name, string? Email)` and `public static KeycloakUser? KeycloakClaims.Read(ClaimsPrincipal user)`: `null` when there's no `sub`.
- Produces: `public static IServiceCollection AddAuthFeature(this IServiceCollection services, IConfiguration configuration)` and `public static WebApplication MapAuthEndpoints(this WebApplication app)` (maps `POST /logout`).

- [ ] **Step 1: Write the failing tests**

```csharp
// KeycloakOptionsTests
IsComplete_when_all_three_set()          // "https://sso/realms/x", "app", "s3cret" → true
IsComplete_false_when_any_blank(string authority, string clientId, string secret)
    // [Theory] ("", "app", "s"), ("https://sso", " ", "s"), ("https://sso", "app", "") → false

// KeycloakClaimsTests  (build ClaimsPrincipal from ClaimsIdentity with raw claim types "sub", "name", …)
Read_uses_sub_name_and_email()           // sub "abc", name "Anna Peeters", email "anna@org.be" → ("abc", "Anna Peeters", "anna@org.be")
Read_falls_back_to_preferred_username()  // sub "abc", preferred_username "apeeters", no name → Name "apeeters"
Read_without_email_gives_null_email()    // sub + name only → Email null
Read_without_sub_returns_null()          // name + email, no sub → null
Read_with_neither_name_nor_username_uses_sub()  // sub "abc" only → Name "abc"
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Booking.Web.Tests`
Expected: build error, types don't exist.

- [ ] **Step 3: Implement `KeycloakOptions` and `KeycloakClaims`** in `src/Booking.Web/Auth/`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/Booking.Web.Tests`
Expected: all pass.

- [ ] **Step 5: Add the package** `Microsoft.AspNetCore.Authentication.OpenIdConnect` 10.0.15 to `Booking.Web.csproj`.

- [ ] **Step 6: Implement `AuthFeature.AddAuthFeature`.**
  - Bind `KeycloakOptions` with `AddOptions<KeycloakOptions>().BindConfiguration("Keycloak").Validate(o => o.IsComplete, "Keycloak:Authority, Keycloak:ClientId and Keycloak:ClientSecret must be set.").ValidateOnStart()`.
  - `AddAuthentication` with default scheme Cookies, challenge scheme OpenIdConnect; `.AddCookie()`; `.AddOpenIdConnect()` configured from `KeycloakOptions` via `AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme).Configure<IOptions<KeycloakOptions>>(…)` with the Global Constraints values (code flow, scopes, `SaveTokens`, `ResponseMode = "query"`, `MapInboundClaims = false`, `TokenValidationParameters.NameClaimType = "name"`).
  - `AddAuthorization` with `FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`.
  - `AddCascadingAuthenticationState()`.
  - Register `IPersonDirectory` → `PersonDirectory` (scoped) here; Task 5 uses it.

- [ ] **Step 7: Implement `MapAuthEndpoints`**: `POST /logout` signs out of the cookie scheme and the OIDC scheme with `RedirectUri = "/"`, so the browser goes through Keycloak's end-session endpoint and comes back to `/` (which asks for login again). The endpoint must validate antiforgery: bind a form value (e.g. `[FromForm] string? returnUrl`, ignored) so minimal APIs enforce the token.

- [ ] **Step 8: Wire it in `Program.cs`.** `builder.Services.AddAuthFeature(builder.Configuration);` next to the other features. In the pipeline: `app.UseAuthentication(); app.UseAuthorization();` after `UseStatusCodePagesWithReExecute` and before `UseAntiforgery()`; `app.MapStaticAssets().AllowAnonymous();` so CSS/JS/favicon load for the login redirect; `app.MapAuthEndpoints();`. Update the "Don't change this file after the skeleton" comment: that rule was for the build day.

- [ ] **Step 9: Config and Docker.**
  - `appsettings.json`: add `"Keycloak": { "Authority": "", "ClientId": "", "ClientSecret": "" }` (empty on purpose).
  - `docker-compose.yml` `app.environment`: `Keycloak__Authority: ${KEYCLOAK_AUTHORITY}`, `Keycloak__ClientId: ${KEYCLOAK_CLIENT_ID}`, `Keycloak__ClientSecret: ${KEYCLOAK_CLIENT_SECRET}`.
  - `.env.example`: those three variables with empty values, plus comments: copy to `.env`; for `dotnet run` use `dotnet user-secrets set "Keycloak:ClientSecret" "…" --project src/Booking.Web` instead; redirect URIs to register (Global Constraints); production behind HTTPS needs `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`.

- [ ] **Step 10: Verify the fail-fast**

Run: `docker compose up --build -d app` with no `.env`, then `docker compose logs app`.
Expected: app exits with an `OptionsValidationException` containing `Keycloak:Authority, Keycloak:ClientId and Keycloak:ClientSecret must be set.`

- [ ] **Step 11: Build and test**

Run: `dotnet build Dotnetjes.slnx` then `dotnet test`
Expected: 0 warnings, all pass. (Login against the real Keycloak is verified in Task 6.)

- [ ] **Step 12: Commit**

```bash
git add src/Booking.Web tests/Booking.Web.Tests docker-compose.yml .env.example
git commit -m "feat: require Keycloak login for every page"
```

---

### Task 5: Current user from the login

**Files:**
- Modify: `src/Booking.Web/CurrentUser/ICurrentUser.cs`
- Create: `src/Booking.Web/CurrentUser/ClaimsCurrentUser.cs`
- Delete: `src/Booking.Web/CurrentUser/LocalStorageCurrentUser.cs`, `src/Booking.Web/Components/Layout/UserPicker.razor`
- Create: `src/Booking.Web/Components/Layout/UserMenu.razor`
- Modify: `src/Booking.Web/Components/Layout/MainLayout.razor`, `src/Booking.Web/Components/Pages/Day.razor`, `src/Booking.Web/Setup/DayViewFeature.cs`
- Test: `tests/Booking.Web.Tests/CurrentUser/ClaimsCurrentUserTests.cs`

**Interfaces:**
- Consumes: `IPersonDirectory.SignInAsync` (Task 1), `KeycloakClaims.Read` (Task 4).
- Produces: `ICurrentUser` reduced to `Task<int?> GetPersonIdAsync();` (no `SetAsync`, `ClearAsync`, `Changed`). Update its doc comment: the person comes from the Keycloak login.
- Produces: `public sealed class ClaimsCurrentUser(AuthenticationStateProvider auth, IPersonDirectory people) : ICurrentUser`, scoped: on the first call reads the user via `KeycloakClaims.Read`, calls `SignInAsync`, caches the person id for the circuit; returns `null` when there's no `sub`.

- [ ] **Step 1: Write the failing tests** (fake `AuthenticationStateProvider` returning a fixed principal; fake `IPersonDirectory` that records calls and returns `new Person { Id = 42, Name = name }`):

```csharp
Returns_person_id_from_directory()          // sub "abc", name "Anna" → 42; directory called with ("abc", "Anna", null)
Calls_directory_once_per_circuit()          // GetPersonIdAsync() twice → directory called once
Returns_null_without_sub()                  // anonymous principal → null; directory not called
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/Booking.Web.Tests --filter ClaimsCurrentUserTests`
Expected: build error, `ClaimsCurrentUser` doesn't exist.

- [ ] **Step 3: Implement `ClaimsCurrentUser`, shrink `ICurrentUser`, register it** in `DayViewFeature` instead of `LocalStorageCurrentUser`. Delete `LocalStorageCurrentUser.cs`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/Booking.Web.Tests`
Expected: all pass.

- [ ] **Step 5: Replace the picker.** Delete `UserPicker.razor`. New `UserMenu.razor`: inside `<AuthorizeView>`, show the name from `KeycloakClaims.Read(context.User)` and a `<form method="post" action="logout">` with `<AntiforgeryToken />` and a `btn btn-outline-secondary btn-sm` **Log out** submit button. `MainLayout.razor`: `<UserPicker />` → `<UserMenu />`. Add `@using Microsoft.AspNetCore.Components.Authorization` to `_Imports.razor`.

- [ ] **Step 6: Clean up `Day.razor`.** Remove the `Changed` subscription, `OnCurrentUserChanged`, `IDisposable`/`Dispose`, and the "Pick who you are to book." block (lines 19-22). `_personId` is still read once in `OnInitializedAsync`.

- [ ] **Step 7: Build and test**

Run: `dotnet build Dotnetjes.slnx` then `dotnet test`
Expected: 0 warnings, all pass; `grep -r "LocalStorage\|UserPicker\|Pick who you are" src` finds nothing.

- [ ] **Step 8: Commit**

```bash
git add -A src tests
git commit -m "feat: current user comes from the Keycloak login"
```

---

### Task 6: Verify against the real Keycloak

Needs the Keycloak authority, client id and secret from the user, and the redirect URIs from Global Constraints registered on the client. **Blocked until the user provides them.**

- [ ] **Step 1: Configure.** Copy `.env.example` to `.env` and fill it in. Run `docker compose up --build -d`.

- [ ] **Step 2: Login and booking.** Open `http://localhost:8080` in a private window → redirected to Keycloak → log in → back on the day view; header shows your name and **Log out**. Book a desk → row shows "You". Admin → People lists you with your Keycloak email.

- [ ] **Step 3: No duplicates.** Log out → you land on the Keycloak login page. Log in again → Admin → People still lists you once.

- [ ] **Step 4: Deactivated (Review Focus 4).** Admin → People → untick yourself. Reload. Book something → "Your profile is inactive — ask an admin." Log out and in again → still inactive. Tick yourself active again.

- [ ] **Step 5: Database reset (Review Focus 2).** Stay logged in. `docker compose down`, delete `.container-data/postgres`, `docker compose up -d`. Reload → day view works, you're listed again in People, booking works.

- [ ] **Step 6: Logged out access.** In a fresh private window, open `http://localhost:8080/admin/people` → redirected to Keycloak, not shown the page.

- [ ] **Step 7: Record the result** in the PR description (which steps passed, anything odd). No commit needed unless fixes were made.
