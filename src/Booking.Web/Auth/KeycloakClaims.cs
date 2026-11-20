using System.Security.Claims;

namespace Booking.Web.Auth;

/// <summary>Who the Keycloak login says you are.</summary>
public sealed record KeycloakUser(string ExternalId, string Name, string? Email);

/// <summary>Reads Keycloak's raw claim names (inbound claim mapping is off, see <see cref="Setup.AuthFeature"/>).</summary>
public static class KeycloakClaims
{
    /// <summary>Null when the user has no <c>sub</c> (not logged in).</summary>
    public static KeycloakUser? Read(ClaimsPrincipal user)
    {
        var sub = Value(user, "sub");
        if (sub is null)
        {
            return null;
        }

        var name = Value(user, "name") ?? Value(user, "preferred_username") ?? sub;
        return new KeycloakUser(sub, name, Value(user, "email"));
    }

    private static string? Value(ClaimsPrincipal user, string type) =>
        user.FindFirst(type)?.Value is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;
}
