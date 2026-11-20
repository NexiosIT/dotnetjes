
namespace Booking.Core.People;

/// <summary>The person behind a login, as the app knows them now.</summary>
public sealed record SignedInPerson(int Id, string Name, string? Email, bool IsActive, string? ExternalId)
{
    public static SignedInPerson From(Person person) => new(person.Id, person.Name, person.Email, person.IsActive, person.ExternalId);
}

/// <summary>Finds or creates the person behind a Keycloak login.</summary>
public interface IPersonDirectory
{
    /// <summary>
    /// Returns the person with this Keycloak user id, creating an active one on first sign-in.
    /// Name and email are refreshed from Keycloak every time; <see cref="Person.IsActive"/> is never changed.
    /// People are matched only by <paramref name="externalId"/>, never by email.
    /// </summary>
    Task<SignedInPerson> SignInAsync(string externalId, string name, string? email, CancellationToken ct = default);
}
