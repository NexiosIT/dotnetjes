using Booking.Core.Entities;

namespace Booking.Core.People;

/// <summary>Finds or creates the person behind a Keycloak login.</summary>
public interface IPersonDirectory
{
    /// <summary>
    /// Returns the person with this Keycloak user id, creating an active one on first sign-in.
    /// Name and email are refreshed from Keycloak every time; <see cref="Person.IsActive"/> is never changed.
    /// People are matched only by <paramref name="externalId"/>, never by email.
    /// </summary>
    Task<Person> SignInAsync(string externalId, string name, string? email, CancellationToken ct = default);
}
