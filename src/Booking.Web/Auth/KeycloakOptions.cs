namespace Booking.Web.Auth;

/// <summary>
/// The organisation's Keycloak client, from config section <c>Keycloak</c>.
/// The secret never goes in appsettings: use <c>dotnet user-secrets</c> or the <c>.env</c> file.
/// </summary>
public sealed class KeycloakOptions
{
    public const string Section = "Keycloak";

    /// <summary>Realm URL, e.g. <c>https://sso.example.org/realms/nexios</c>.</summary>
    public string Authority { get; set; } = "";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Authority) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);
}
