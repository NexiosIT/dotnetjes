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

/// <summary>Login through the organisation's Keycloak. Every page requires it.</summary>
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
            .AddCookie()
            .AddOpenIdConnect();

        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IOptions<KeycloakOptions>>((oidc, keycloak) =>
            {
                oidc.Authority = keycloak.Value.Authority;
                oidc.ClientId = keycloak.Value.ClientId;
                oidc.ClientSecret = keycloak.Value.ClientSecret;
                oidc.ResponseType = OpenIdConnectResponseType.Code;
                // form_post needs SameSite=None cookies, which browsers drop without HTTPS (dev runs on http://localhost).
                oidc.ResponseMode = OpenIdConnectResponseMode.Query;
                oidc.Scope.Clear();
                oidc.Scope.Add("openid");
                oidc.Scope.Add("profile");
                oidc.Scope.Add("email");
                oidc.SaveTokens = true; // the id token is sent as id_token_hint on logout
                oidc.MapInboundClaims = false; // keep Keycloak's names: sub, name, email, preferred_username
                oidc.TokenValidationParameters.NameClaimType = "name";
            });

        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddCascadingAuthenticationState();

        services.AddScoped<IPersonDirectory, PersonDirectory>();
        return services;
    }

    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        // Binding a form value makes minimal APIs validate the antiforgery token.
        app.MapPost("/logout", ([FromForm] string? returnUrl) => TypedResults.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
        return app;
    }
}
