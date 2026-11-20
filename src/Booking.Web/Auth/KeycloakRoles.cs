using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Booking.Web.Auth;

/// <summary>
/// Reads Keycloak client roles (<c>resource_access.&lt;clientId&gt;.roles</c>) from an access token.
/// Keycloak puts them in the access token, not in the ID token the login reads its claims from.
/// </summary>
public static class KeycloakRoles
{
    /// <summary>The roles of <paramref name="clientId"/>; empty when the token is missing or not shaped as expected.</summary>
    public static IReadOnlyList<string> ClientRoles(string? accessToken, string clientId)
    {
        // No signature check: the token came straight from Keycloak's token endpoint over the back channel.
        var parts = accessToken?.Split('.');
        if (parts is not { Length: 3 })
        {
            return [];
        }

        try
        {
            using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[1]));
            if (payload.RootElement.ValueKind != JsonValueKind.Object ||
                !payload.RootElement.TryGetProperty("resource_access", out var access) || access.ValueKind != JsonValueKind.Object ||
                !access.TryGetProperty(clientId, out var client) || client.ValueKind != JsonValueKind.Object ||
                !client.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return roles.EnumerateArray()
                .Where(r => r.ValueKind == JsonValueKind.String)
                .Select(r => r.GetString()!)
                .ToList();
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return [];
        }
    }
}
