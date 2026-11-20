using System.Security.Claims;
using Booking.Core.People;
using Booking.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Booking.Web.Setup;

/// <summary>
/// Login through the organisation's Keycloak. Every page requires it; the Admin pages also require
/// the Keycloak client role <see cref="AdminAccess.Role"/> (policy <see cref="AdminAccess.Policy"/>).
/// </summary>
public static class AuthFeature
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<KeycloakOptions>()
            .BindConfiguration(KeycloakOptions.Section)
            .Validate(o => o.IsComplete, "Keycloak:Authority, Keycloak:ClientId and Keycloak:ClientSecret must be set.")
            .ValidateOnStart();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(cookie =>
            {
                cookie.AccessDeniedPath = "/access-denied";
                cookie.ExpireTimeSpan = TimeSpan.FromHours(10);
                cookie.SlidingExpiration = false;
            })
            .AddOpenIdConnect();

        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IOptions<KeycloakOptions>>((oidc, keycloak) =>
            {
                oidc.Authority = keycloak.Value.Authority;
                oidc.ClientId = keycloak.Value.ClientId;
                oidc.ClientSecret = keycloak.Value.ClientSecret;
                oidc.ResponseType = OpenIdConnectResponseType.Code;
                oidc.ResponseMode = OpenIdConnectResponseMode.Query;
                oidc.CorrelationCookie.SameSite = SameSiteMode.Lax;
                oidc.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                oidc.NonceCookie.SameSite = SameSiteMode.Lax;
                oidc.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                oidc.Events.OnRemoteFailure = HandleRemoteFailureAsync;
                oidc.Scope.Clear();
                oidc.Scope.Add("openid");
                oidc.Scope.Add("profile");
                oidc.Scope.Add("email");
                oidc.SaveTokens = true;
                oidc.MapInboundClaims = false;
                oidc.TokenValidationParameters.NameClaimType = "name";
                oidc.TokenValidationParameters.RoleClaimType = AdminAccess.RoleClaimType;
                oidc.Events.OnTokenValidated = AddRolesAsync;
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(AdminAccess.Policy, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                context.User.Claims.Any(c => c.Type == AdminAccess.RoleClaimType && string.Equals(c.Value, AdminAccess.Role, StringComparison.OrdinalIgnoreCase))));
        });
        services.AddCascadingAuthenticationState();

        services.AddScoped<IPersonDirectory, PersonDirectory>();
        return services;
    }

    /// <summary>
    /// A failed callback (wrong secret, bad issuer, lost correlation cookie…) would otherwise go to /Error,
    /// which needs login, which redirects to Keycloak, which sends the user straight back: an endless loop.
    /// Answer with a plain error page instead and log the real reason.
    /// </summary>
    public static async Task HandleRemoteFailureAsync(RemoteFailureContext context)
    {
        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(AuthFeature))
            .LogError(context.Failure, "Keycloak login failed");

        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("Login failed. Please try again later or contact the administrator.");
    }

    /// <summary>
    /// Keycloak puts client roles only in the access token, not in the ID token the login identity is built from.
    /// Copy this client's roles onto the identity so they end up in the login cookie. Read once per login.
    /// Any role claim already on the identity (e.g. from a future ID-token mapper) is dropped: only this client's roles count.
    /// </summary>
    public static Task AddRolesAsync(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is ClaimsIdentity identity)
        {
            foreach (var existing in identity.FindAll(identity.RoleClaimType).ToList())
            {
                identity.RemoveClaim(existing);
            }

            foreach (var role in KeycloakRoles.ClientRoles(context.TokenEndpointResponse?.AccessToken, context.Options.ClientId ?? ""))
            {
                identity.AddClaim(new Claim(identity.RoleClaimType, role));
            }
        }
        return Task.CompletedTask;
    }

    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/logout", ([FromForm] string? returnUrl) => TypedResults.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
        return app;
    }
}
