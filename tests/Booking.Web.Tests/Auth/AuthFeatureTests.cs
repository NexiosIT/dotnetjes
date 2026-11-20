using Booking.Web.Setup;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Booking.Web.Tests.Auth;

public class AuthFeatureTests
{
    private static OpenIdConnectOptions ConfiguredOidc()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:Authority"] = "https://sso.example.org/realms/test",
            ["Keycloak:ClientId"] = "app",
            ["Keycloak:ClientSecret"] = "secret",
        }).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(config).AddLogging();
        services.AddAuthFeature(config);
        return services.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
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
}
