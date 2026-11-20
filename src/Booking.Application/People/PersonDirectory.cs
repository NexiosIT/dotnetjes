namespace Booking.Application.People;

public sealed class PersonDirectory(IUnitOfWorkFactory unitsOfWork) : IPersonDirectory
{
    /// <summary>
    /// Two first logins at the same moment (two tabs) both find nobody; the unique Keycloak-id index lets one insert win,
    /// and the other then signs in as that person.
    /// </summary>
    public async Task<SignedInPerson> SignInAsync(string externalId, string name, string? email, CancellationToken ct = default)
    {
        if (await TryRefreshAsync(externalId, name, email, ct) is { } existing)
        {
            return existing;
        }

        await using var uow = await unitsOfWork.CreateAsync(ct);
        var person = Person.FromLogin(externalId, name, email);
        uow.People.Add(person);
        return await uow.SaveChangesAsync(ct) switch
        {
            null => SignedInPerson.From(person),
            SaveConflict.ExternalIdTaken => await TryRefreshAsync(externalId, name, email, ct)
                ?? throw new InvalidOperationException($"Person with external id '{externalId}' vanished during sign-in."),
            var other => throw new InvalidOperationException($"Unexpected conflict during sign-in: {other}")
        };
    }

    private async Task<SignedInPerson?> TryRefreshAsync(string externalId, string name, string? email, CancellationToken ct)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);
        var person = await uow.People.FindByExternalIdAsync(externalId, ct);
        if (person is null)
        {
            return null;
        }

        person.RefreshProfile(name, email);
        await uow.SaveChangesAsync(ct);
        return SignedInPerson.From(person);
    }
}
