using System.Security.Claims;
using Booking.Web.Auth;

namespace Booking.Web.Tests.Auth;

public class KeycloakClaimsTests
{
    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void GivenSubNameAndEmailClaims_WhenReadingTheUser_ThenAllThreeAreUsed() =>
        Assert.Equal(new KeycloakUser("abc", "Anna Peeters", "anna@org.be"),
            KeycloakClaims.Read(User(("sub", "abc"), ("name", "Anna Peeters"), ("email", "anna@org.be"))));

    [Fact]
    public void GivenNoNameClaim_WhenReadingTheUser_ThenThePreferredUsernameIsTheName() =>
        Assert.Equal("apeeters", KeycloakClaims.Read(User(("sub", "abc"), ("preferred_username", "apeeters")))!.Name);

    [Fact]
    public void GivenNoEmailClaim_WhenReadingTheUser_ThenTheEmailIsNull() =>
        Assert.Null(KeycloakClaims.Read(User(("sub", "abc"), ("name", "Anna")))!.Email);

    [Fact]
    public void GivenNoSubClaim_WhenReadingTheUser_ThenNullIsReturned() =>
        Assert.Null(KeycloakClaims.Read(User(("name", "Anna"), ("email", "anna@org.be"))));

    [Fact]
    public void GivenNeitherNameNorUsername_WhenReadingTheUser_ThenTheSubIsTheName() =>
        Assert.Equal("abc", KeycloakClaims.Read(User(("sub", "abc")))!.Name);
}
