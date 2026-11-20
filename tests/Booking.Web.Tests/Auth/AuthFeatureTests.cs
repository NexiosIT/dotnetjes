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
    public void WhenConfiguringTheLogin_ThenCorrelationAndNonceCookiesAreLaxAndWorkOverPlainHttp()
    {
        var oidc = ConfiguredOidc();

        Assert.Equal(SameSiteMode.Lax, oidc.CorrelationCookie.SameSite);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, oidc.CorrelationCookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, oidc.NonceCookie.SameSite);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, oidc.NonceCookie.SecurePolicy);
    }

    [Fact]
    public async Task GivenAFailedRemoteLogin_WhenHandlingTheFailure_ThenA500ErrorPageIsShownInsteadOfRedirecting()
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
    public void WhenConfiguringTheLogin_ThenTheRoleClaimTypeIsTheAdminRoleClaimType() =>
        Assert.Equal(AdminAccess.RoleClaimType, ConfiguredOidc().TokenValidationParameters.RoleClaimType);

    [Fact]
    public async Task WhenAuthorizingForTheAdminPolicy_ThenOnlyTheAdminRoleSucceeds()
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
    public async Task GivenTheAdminRoleInAnyLetterCase_WhenAuthorizingForTheAdminPolicy_ThenItSucceeds(string role) =>
        Assert.True((await ConfiguredServices().GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User(role), AdminAccess.Policy)).Succeeded);

    [Fact]
    public void WhenConfiguringTheCookie_ThenForbiddenUsersAreSentToAccessDenied() =>
        Assert.Equal("/access-denied", ConfiguredServices()
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme).AccessDeniedPath.Value);

    [Fact]
    public void WhenConfiguringTheCookie_ThenLoginLastsTenHoursAndIsNotRenewed()
    {
        var cookie = ConfiguredServices()
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal(TimeSpan.FromHours(10), cookie.ExpireTimeSpan);
        Assert.False(cookie.SlidingExpiration);
    }

    [Fact]
    public async Task GivenAnAccessTokenWithClientRoles_WhenTheTokenIsValidated_ThenTheyBecomeRoleClaims()
    {
        var context = TokenValidated(TestTokens.WithPayload("""{"resource_access":{"app":{"roles":["Admin"]}}}"""));

        await AuthFeature.AddRolesAsync(context);

        Assert.True(context.Principal!.IsInRole("Admin"));
    }

    [Fact]
    public async Task GivenAnIdTokenRoleClaim_WhenTheTokenIsValidated_ThenOnlyAccessTokenRolesAreKept()
    {
        var context = TokenValidated(TestTokens.WithPayload("""{"resource_access":{"app":{"roles":["viewer"]}}}"""));
        ((ClaimsIdentity)context.Principal!.Identity!).AddClaim(new Claim(AdminAccess.RoleClaimType, "Admin"));

        await AuthFeature.AddRolesAsync(context);

        Assert.False(context.Principal.IsInRole("Admin"));
        Assert.True(context.Principal.IsInRole("viewer"));
    }

    [Fact]
    public async Task GivenNoAccessToken_WhenTheTokenIsValidated_ThenNoRoleClaimsAreAdded()
    {
        var context = TokenValidated(null);

        await AuthFeature.AddRolesAsync(context);

        Assert.DoesNotContain(context.Principal!.Claims, c => c.Type == AdminAccess.RoleClaimType);
    }
}
