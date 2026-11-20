namespace Booking.Web.CurrentUser;

/// <summary>
/// Who is using this browser. The only way the app knows the current person,
/// so real login (Entra ID) can replace it later without touching pages.
/// Scoped: one instance per browser session (Blazor circuit).
/// </summary>
public interface ICurrentUser
{
    /// <summary>The chosen person, or null when nobody has been picked yet.</summary>
    Task<int?> GetPersonIdAsync();

    Task SetAsync(int personId);

    Task ClearAsync();

    /// <summary>Raised after <see cref="SetAsync"/> or <see cref="ClearAsync"/> so other components can re-render.</summary>
    event Action? Changed;
}
