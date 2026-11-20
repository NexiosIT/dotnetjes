# Admin Role Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Only users with the Keycloak client role `Admin` can see and open the Admin pages; others get a "no access" page.

**Architecture:** At login (OIDC `OnTokenValidated`) the app reads `resource_access.<ClientId>.roles` from the access token and adds them as `role` claims to the cookie identity. A named policy `"Admin"` requires the `Admin` role. The Admin pages carry `[Authorize(Policy = "Admin")]`, the router becomes `AuthorizeRouteView` so in-app navigation is checked too, and the menu link sits in `<AuthorizeView Policy="Admin">`.

**Tech Stack:** .NET 10, Blazor Web App (Interactive Server, `prerender: false`), `Microsoft.AspNetCore.Authentication.OpenIdConnect` 10.0.15 (already referenced), System.Text.Json, xUnit v2.

**Spec:** `docs/superpowers/specs/2026-11-20-admin-role-design.md`. Read it first.

## Global Constraints

- Role name **`Admin`**, policy name **`"Admin"`**, role claim type **`"role"`**: defined once as constants in `src/Booking.Web/Auth/AdminAccess.cs`, used everywhere else.
- Roles come from the **client** roles of `Keycloak:ClientId` in the **access token**: `resource_access.<clientId>.roles`. Realm roles (`realm_access`) are ignored.
- Role match is exact (ordinal, case-sensitive), as ASP.NET's `IsInRole` does.
- Missing, empty or malformed token / `resource_access` → no roles, never an exception. The user still logs in.
- No new NuGet packages. No Keycloak mapper changes required.
- No-access page route: **`/access-denied`**; cookie `AccessDeniedPath` points at it.
- Keep `MapInboundClaims = false` and `NameClaimType = "name"` as they are.

## Review Focus

1. **Non-admin clicks around inside the app to `/admin/...`** (no full page load) → must get the no-access content, because Blazor client-side routing skips the HTTP authorization check. *(Task 3: `AuthorizeRouteView`; manual check in Task 3 Step 6)*
2. **`Admin` role assigned on a different client, or as a realm role** → not admin. *(Task 1: `Ignores_roles_of_other_clients`, `Ignores_realm_roles`)*
3. **Access token missing or garbage** (null, not three segments, bad base64, JSON without `resource_access`) → no roles, login still succeeds. *(Task 1: `Malformed_or_missing_token_gives_no_roles`; Task 2: `Token_validated_without_access_token_adds_no_roles`)*
4. **Role removed in Keycloak while the person is logged in** → stays admin until next login (accepted in spec). Documented in `.env.example`, no test.
5. **Both Admin pages protected, not just one** → *(Task 3: `Admin_pages_require_admin_policy`)*

---

### Task 1: Read client roles from the access token

**Files:**
- Create: `src/Booking.Web/Auth/AdminAccess.cs`
- Create: `src/Booking.Web/Auth/KeycloakRoles.cs`
- Test: `tests/Booking.Web.Tests/Auth/KeycloakRolesTests.cs`

**Interfaces:**
- Produces:
  - `public static class AdminAccess { public const string Role = "Admin"; public const string Policy = "Admin"; public const string RoleClaimType = "role"; }`
  - `public static class KeycloakRoles { public static IReadOnlyList<string> ClientRoles(string? accessToken, string clientId); }`

- [ ] **Step 1: Write the failing tests**

Test helper builds an unsigned JWT: `static string Token(string payloadJson) => "e30." + Base64Url(payloadJson) + ".sig";` (base64url = base64 with `+`→`-`, `/`→`_`, no `=` padding).

```csharp
[Fact]
public void Reads_roles_of_this_client() =>
    Assert.Equal(["Admin", "other"], KeycloakRoles.ClientRoles(
        Token("""{"resource_access":{"booking":{"roles":["Admin","other"]}}}"""), "booking"));

[Fact]
public void Ignores_roles_of_other_clients() =>
    Assert.Empty(KeycloakRoles.ClientRoles(
        Token("""{"resource_access":{"account":{"roles":["Admin"]}}}"""), "booking"));

[Fact]
public void Ignores_realm_roles() =>
    Assert.Empty(KeycloakRoles.ClientRoles(
        Token("""{"realm_access":{"roles":["Admin"]}}"""), "booking"));

[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("not-a-jwt")]
[InlineData("a.!!!.c")]
public void Malformed_or_missing_token_gives_no_roles(string? token) =>
    Assert.Empty(KeycloakRoles.ClientRoles(token, "booking"));

[Theory]
[InlineData("""{}""")]
[InlineData("""{"resource_access":"oops"}""")]
[InlineData("""{"resource_access":{"booking":{}}}""")]
[InlineData("""{"resource_access":{"booking":{"roles":"Admin"}}}""")]
[InlineData("""{"resource_access":{"booking":{"roles":[1,null,"Admin"]}}}""")]
public void Unexpected_shapes_give_only_string_roles(string payload) =>
    Assert.DoesNotContain(KeycloakRoles.ClientRoles(Token(payload), "booking"), r => r != "Admin");
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Booking.Web.Tests --filter "FullyQualifiedName~KeycloakRolesTests"`
Expected: build FAIL, `KeycloakRoles` does not exist.

- [ ] **Step 3: Implement `AdminAccess` and `KeycloakRoles.ClientRoles`**

Split on `.`, require 3 parts, base64url-decode the middle (`Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes`, already available via the OIDC package), parse with `JsonDocument`, walk `resource_access` → `clientId` → `roles`, keep only string elements. Check `ValueKind` at every level; catch `FormatException`/`JsonException`/`ArgumentException` and return empty. No signature validation: the token came straight from Keycloak's token endpoint over the back channel (say so in a one-line comment).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Booking.Web.Tests --filter "FullyQualifiedName~KeycloakRolesTests"`
Expected: PASS (all).

- [ ] **Step 5: Commit**

```bash
git add src/Booking.Web/Auth/AdminAccess.cs src/Booking.Web/Auth/KeycloakRoles.cs tests/Booking.Web.Tests/Auth/KeycloakRolesTests.cs
git commit -m "feat: read Keycloak client roles from the access token"
```

### Task 2: Roles into the login, Admin policy, access-denied path

**Files:**
- Modify: `src/Booking.Web/Setup/AuthFeature.cs`
- Test: `tests/Booking.Web.Tests/Auth/AuthFeatureTests.cs`

**Interfaces:**
- Consumes: `AdminAccess.*`, `KeycloakRoles.ClientRoles(string?, string)` (Task 1).
- Produces: `public static Task AddRolesAsync(TokenValidatedContext context)` on `AuthFeature`; policy `AdminAccess.Policy` registered; cookie `AccessDeniedPath = "/access-denied"`.

- [ ] **Step 1: Write the failing tests** (in `AuthFeatureTests`, reusing the existing `ConfiguredOidc()` pattern; `ClientId` there is `"app"`)

```csharp
[Fact]
public void Role_claims_use_the_role_type() =>
    Assert.Equal(AdminAccess.RoleClaimType, ConfiguredOidc().TokenValidationParameters.RoleClaimType);

[Fact]
public async Task Admin_policy_requires_the_Admin_role()
{
    // Build the provider like ConfiguredOidc(), get IAuthorizationService.
    // user with ("sub","x") + (role,"Admin"), identity RoleClaimType = "role", authType "test" → Succeeded
    // same user without the role claim → not Succeeded
}

[Fact]
public void Cookie_sends_forbidden_users_to_access_denied() =>
    // IOptionsMonitor<CookieAuthenticationOptions>.Get(CookieAuthenticationDefaults.AuthenticationScheme)
    Assert.Equal("/access-denied", cookieOptions.AccessDeniedPath.Value);

[Fact]
public async Task Token_validated_adds_client_roles_as_role_claims()
{
    // TokenValidatedContext(http, scheme, new OpenIdConnectOptions { ClientId = "app" },
    //   principal with ClaimsIdentity(claims, "test", "name", "role"), new AuthenticationProperties())
    // { TokenEndpointResponse = new OpenIdConnectMessage { AccessToken = <token with resource_access.app.roles ["Admin"]> } }
    await AuthFeature.AddRolesAsync(context);
    Assert.True(context.Principal!.IsInRole("Admin"));
}

[Fact]
public async Task Token_validated_without_access_token_adds_no_roles()
{
    // same, TokenEndpointResponse = null
    await AuthFeature.AddRolesAsync(context);
    Assert.DoesNotContain(context.Principal!.Claims, c => c.Type == "role");
}
```

Move the JWT-building helper from Task 1 into a small shared `tests/Booking.Web.Tests/Auth/TestTokens.cs` (`public static string WithPayload(string json)`) and use it from both test classes.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Booking.Web.Tests --filter "FullyQualifiedName~AuthFeatureTests"`
Expected: build FAIL (`AddRolesAsync` missing), then assertion failures.

- [ ] **Step 3: Implement in `AuthFeature`**

- In the OIDC `Configure`: `oidc.TokenValidationParameters.RoleClaimType = AdminAccess.RoleClaimType;` and `oidc.Events.OnTokenValidated = AddRolesAsync;`
- `AddRolesAsync`: take `context.Principal?.Identity as ClaimsIdentity`; for each role from `KeycloakRoles.ClientRoles(context.TokenEndpointResponse?.AccessToken, context.Options.ClientId!)` add `new Claim(identity.RoleClaimType, role)`. Returns `Task.CompletedTask`.
- `.AddCookie(o => o.AccessDeniedPath = "/access-denied")`.
- `AddAuthorization`: add `options.AddPolicy(AdminAccess.Policy, p => p.RequireAuthenticatedUser().RequireRole(AdminAccess.Role));` next to the existing fallback policy.
- Update the class summary: every page requires login; Admin pages also require the Keycloak client role `Admin`.

- [ ] **Step 4: Run all Web tests**

Run: `dotnet test tests/Booking.Web.Tests`
Expected: PASS (all, including the existing ones).

- [ ] **Step 5: Commit**

```bash
git add src/Booking.Web/Setup/AuthFeature.cs tests/Booking.Web.Tests/Auth/
git commit -m "feat: Admin policy from the Keycloak client role"
```

### Task 3: Lock the Admin pages, hide the link, no-access page

**Files:**
- Create: `src/Booking.Web/Components/Pages/AccessDenied.razor`
- Modify: `src/Booking.Web/Components/Pages/Admin/Resources.razor`, `src/Booking.Web/Components/Pages/Admin/People.razor`
- Modify: `src/Booking.Web/Components/Routes.razor`
- Modify: `src/Booking.Web/Components/Layout/NavMenu.razor`
- Modify: `src/Booking.Web/Components/_Imports.razor`
- Modify: `.env.example`
- Test: `tests/Booking.Web.Tests/Auth/AdminPagesTests.cs`

**Interfaces:**
- Consumes: `AdminAccess.Policy` (Task 1); policy + `/access-denied` path (Task 2).

- [ ] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData(typeof(Booking.Web.Components.Pages.Admin.Resources))]
[InlineData(typeof(Booking.Web.Components.Pages.Admin.People))]
public void Admin_pages_require_admin_policy(Type page) =>
    Assert.Equal(AdminAccess.Policy, page.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test tests/Booking.Web.Tests --filter "FullyQualifiedName~AdminPagesTests"`
Expected: FAIL (`null` policy).

- [ ] **Step 3: Protect pages and router**

- `_Imports.razor`: add `@using Microsoft.AspNetCore.Authorization` and `@using Booking.Web.Auth`.
- `Resources.razor`, `People.razor`: `@attribute [Authorize(Policy = AdminAccess.Policy)]` under `@page`.
- `Routes.razor`: `RouteView` → `AuthorizeRouteView` (same `RouteData`, `DefaultLayout`), with `<NotAuthorized><AccessDenied /></NotAuthorized>`.

- [ ] **Step 4: No-access page and menu**

- `AccessDenied.razor`: `@page "/access-denied"`, `<PageTitle>No access</PageTitle>`, `<h1>No access</h1>`, text **"You don't have access to the admin pages."**, and a link `href=""` **"Back to booking"**. Plain Bootstrap like `NotFound.razor`.
- `NavMenu.razor`: wrap the Admin `nav-item` div in `<AuthorizeView Policy="@AdminAccess.Policy">…</AuthorizeView>`.
- `.env.example`, in the Keycloak client notes: "Admin pages: create client role `Admin` on this client and assign it (directly or via a group). Takes effect at the person's next login."

- [ ] **Step 5: Run all tests**

Run: `dotnet build` then `dotnet test`
Expected: build with no new warnings; all tests PASS (Core tests need Docker for Testcontainers).

- [ ] **Step 6: Manual check against the real Keycloak**

Run: `dotnet run --project src/Booking.Web`, log in at `http://localhost:5017`.
- User **with** client role `Admin`: Admin link visible; both tabs open; "Send today's summary now" works.
- User **without** it (log out, log in as another user): no Admin link; typing `/admin/resources` → `/access-denied` page; from the day view, navigating to `/admin/people` via browser history/back (in-app navigation) → no-access content, not the page.

- [ ] **Step 7: Commit**

```bash
git add src/Booking.Web/Components tests/Booking.Web.Tests/Auth/AdminPagesTests.cs .env.example
git commit -m "feat: Admin pages only for Keycloak admins"
```
