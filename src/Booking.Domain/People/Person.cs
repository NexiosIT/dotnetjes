namespace Booking.Domain.People;

/// <summary>
/// Someone who can book (aggregate root). Created from Keycloak on first login and matched only by <see cref="ExternalId"/>;
/// name and email follow Keycloak, only <see cref="IsActive"/> is managed in the app.
/// </summary>
public class Person
{
    private Person()
    {
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public string? Email { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>The Keycloak user id (<c>sub</c> claim). Null for people who never logged in.</summary>
    public string? ExternalId { get; private set; }

    public bool HasEmail => !string.IsNullOrWhiteSpace(Email);

    public static Person FromLogin(string externalId, string name, string? email)
    {
        var person = new Person { ExternalId = externalId };
        person.RefreshProfile(name, email);
        return person;
    }

    /// <summary>Name and email as Keycloak has them now; a blank email is stored as null. Never changes <see cref="IsActive"/>.</summary>
    public void RefreshProfile(string name, string? email)
    {
        Name = name.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    /// <summary>A person as stored, for tests that need fixed ids without a database.</summary>
    internal static Person Reconstitute(int id, string name, string? email, bool isActive = true, string? externalId = null) =>
        new() { Id = id, Name = name, Email = email, IsActive = isActive, ExternalId = externalId };

    /// <summary>Inactive people keep their bookings but can't make new ones or join the waitlist.</summary>
    public void SetActive(bool isActive) => IsActive = isActive;
}
