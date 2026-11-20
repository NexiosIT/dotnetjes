namespace Booking.Web.Auth;

/// <summary>Who may open the Admin pages: users with the Keycloak client role <c>Admin</c> on this app's client.</summary>
public static class AdminAccess
{
    /// <summary>The Keycloak client role. Matched case-insensitively: admin, ADMIN and Admin all count.</summary>
    public const string Role = "Admin";

    /// <summary>Authorization policy name for the Admin pages.</summary>
    public const string Policy = "Admin";

    /// <summary>Claim type the roles are stored under in the login cookie.</summary>
    public const string RoleClaimType = "role";
}
