using System.Security.Claims;
using Booking.Web.Auth;
using Booking.Web.Setup;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Booking.Web.Tests.Auth;

public class AuthFeatureTests
{
    private static ServiceProvider ConfiguredServices()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:Authority"] = "https://sso.example.org/realms/test",
            ["Keycloak:ClientId"] = "app",
            ["Keycloak:ClientSecret"] = "secret",
        }).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(config).AddLogging();
        services.AddAuthFeature(config);
        return services.BuildServiceProvider();
    }

    private static OpenIdConnectOptions ConfiguredOidc() =>
        ConfiguredServices()
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);

    private static ClaimsPrincipal User(params string[] roles) =>
        new(new ClaimsIdentity(
            [new Claim("sub", "x"), .. roles.Select(r => new Claim(AdminAccess.RoleClaimType, r))],
            "test", "name", AdminAccess.RoleClaimType));

    private static TokenValidatedContext TokenValidated(string? accessToken)
    {
        var scheme = new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler));
        return new TokenValidatedContext(new DefaultHttpContext(), scheme, new OpenIdConnectOptions { ClientId = "app" },
            User(), new AuthenticationProperties())
        {
            TokenEndpointResponse = accessToken is null ? null : new OpenIdConnectMessage { AccessToken = accessToken }
        };
    }

    [Fact]
    public void Correlation_and_nonce_cookies_work_over_plain_http()
    {
        // The callback is a top-level GET (ResponseMode=query), so Lax cookies are sent; Secure-only
        // cookies would be dropped on http://<host>:8080 and every login would fail.
        var oidc = ConfiguredOidc();

        Assert.Equal(SameSiteMode.Lax, oidc.CorrelationCookie.SameSite);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, oidc.CorrelationCookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, oidc.NonceCookie.SameSite);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, oidc.NonceCookie.SecurePolicy);
    }

    [Fact]
    public async Task Failed_login_shows_an_error_instead_of_redirecting_again()
    {
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
        var scheme = new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, null, typeof(OpenIdConnectHandler));
        var context = new RemoteFailureContext(http, scheme, new OpenIdConnectOptions(), new AuthenticationFailureException("Correlation failed."));

        await AuthFeature.HandleRemoteFailureAsync(context);

        Assert.True(context.Result?.Handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, http.Response.StatusCode);
        http.Response.Body.Position = 0;
        Assert.Contains("Login failed", await new StreamReader(http.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public void Role_claims_use_the_role_type() =>
        Assert.Equal(AdminAccess.RoleClaimType, ConfiguredOidc().TokenValidationParameters.RoleClaimType);

    [Fact]
    public async Task Admin_policy_requires_the_Admin_role()
    {
        var authorization = ConfiguredServices().GetRequiredService<IAuthorizationService>();

        Assert.True((await authorization.AuthorizeAsync(User("Admin"), AdminAccess.Policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(User(), AdminAccess.Policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(User("viewer", "Administrator"), AdminAccess.Policy)).Succeeded);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("aDmIn")]
    public async Task Admin_policy_ignores_case(string role) =>
        Assert.True((await ConfiguredServices().GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User(role), AdminAccess.Policy)).Succeeded);

    [Fact]
    public void Cookie_sends_forbidden_users_to_access_denied() =>
        Assert.Equal("/access-denied", ConfiguredServices()
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme).AccessDeniedPath.Value);

    [Fact]
    public void Login_lasts_one_working_day_without_renewal()
    {
        // Roles are read at login; a fixed lifetime makes a removed Admin role apply by the next day at the latest.
        var cookie = ConfiguredServices()
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal(TimeSpan.FromHours(10), cookie.ExpireTimeSpan);
        Assert.False(cookie.SlidingExpiration);
    }

    [Fact]
    public async Task Token_validated_adds_client_roles_as_role_claims()
    {
        var context = TokenValidated(TestTokens.WithPayload("""{"resource_access":{"app":{"roles":["Admin"]}}}"""));

        await AuthFeature.AddRolesAsync(context);

        Assert.True(context.Principal!.IsInRole("Admin"));
    }

    [Fact]
    public async Task Token_validated_ignores_role_claims_from_the_id_token()
    {
        // A future Keycloak mapper could put a "role" claim in the ID token; only this client's roles may count.
        var context = TokenValidated(TestTokens.WithPayload("""{"resource_access":{"app":{"roles":["viewer"]}}}"""));
        ((ClaimsIdentity)context.Principal!.Identity!).AddClaim(new Claim(AdminAccess.RoleClaimType, "Admin"));

        await AuthFeature.AddRolesAsync(context);

        Assert.False(context.Principal.IsInRole("Admin"));
        Assert.True(context.Principal.IsInRole("viewer"));
    }

    [Fact]
    public async Task Token_validated_without_access_token_adds_no_roles()
    {
        var context = TokenValidated(null);

        await AuthFeature.AddRolesAsync(context);

        Assert.DoesNotContain(context.Principal!.Claims, c => c.Type == AdminAccess.RoleClaimType);
    }
}
