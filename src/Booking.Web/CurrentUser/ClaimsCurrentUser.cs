using Booking.Application.People;
using Booking.Web.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace Booking.Web.CurrentUser;

/// <summary>
/// Resolves the logged-in Keycloak user to a person once per circuit. Going through
/// <see cref="IPersonDirectory.SignInAsync"/> every time (rather than trusting an id in the cookie)
/// means a reset database simply re-creates the person, and name/email stay fresh.
/// </summary>
public sealed class ClaimsCurrentUser(AuthenticationStateProvider auth, IPersonDirectory people) : ICurrentUser
{
    private Task<int?>? _personId;

    public Task<int?> GetPersonIdAsync() => _personId ??= ResolveAsync();

    private async Task<int?> ResolveAsync()
    {
        var state = await auth.GetAuthenticationStateAsync();
        if (KeycloakClaims.Read(state.User) is not { } user)
        {
            return null;
        }

        var person = await people.SignInAsync(user.ExternalId, user.Name, user.Email);
        return person.Id;
    }
}
