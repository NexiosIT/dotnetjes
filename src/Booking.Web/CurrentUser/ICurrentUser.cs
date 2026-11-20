namespace Booking.Web.CurrentUser;

/// <summary>
/// Who is using this browser. The only way pages learn the current person.
/// The person comes from the Keycloak login; there is no switching inside the app.
/// Scoped: one instance per browser session (Blazor circuit).
/// </summary>
public interface ICurrentUser
{
    /// <summary>The logged-in person, or null when nobody is logged in.</summary>
    Task<int?> GetPersonIdAsync();
}
