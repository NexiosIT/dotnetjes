using System.Security.Claims;
using Booking.Application.People;
using Booking.Web.CurrentUser;
using Microsoft.AspNetCore.Components.Authorization;

namespace Booking.Web.Tests.CurrentUser;

public class ClaimsCurrentUserTests
{
    private sealed class FixedAuth(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
    }

    private sealed class RecordingDirectory : IPersonDirectory
    {
        public List<(string ExternalId, string Name, string? Email)> Calls { get; } = [];

        public Task<SignedInPerson> SignInAsync(string externalId, string name, string? email, CancellationToken ct = default)
        {
            Calls.Add((externalId, name, email));
            return Task.FromResult(new SignedInPerson(42, name, email, true, externalId));
        }
    }

    private static ClaimsPrincipal LoggedIn(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public async Task Returns_person_id_from_directory()
    {
        var directory = new RecordingDirectory();
        var currentUser = new ClaimsCurrentUser(new FixedAuth(LoggedIn(("sub", "abc"), ("name", "Anna"))), directory);

        Assert.Equal(42, await currentUser.GetPersonIdAsync());
        Assert.Equal([("abc", "Anna", (string?)null)], directory.Calls);
    }

    [Fact]
    public async Task Calls_directory_once_per_circuit()
    {
        var directory = new RecordingDirectory();
        var currentUser = new ClaimsCurrentUser(new FixedAuth(LoggedIn(("sub", "abc"), ("name", "Anna"))), directory);

        await currentUser.GetPersonIdAsync();
        await currentUser.GetPersonIdAsync();

        Assert.Single(directory.Calls);
    }

    [Fact]
    public async Task Returns_null_without_sub()
    {
        var directory = new RecordingDirectory();
        var currentUser = new ClaimsCurrentUser(new FixedAuth(new ClaimsPrincipal(new ClaimsIdentity())), directory);

        Assert.Null(await currentUser.GetPersonIdAsync());
        Assert.Empty(directory.Calls);
    }
}
