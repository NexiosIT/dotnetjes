using System.Security.Claims;
using Booking.Web.Auth;

namespace Booking.Web.Tests.Auth;

public class KeycloakClaimsTests
{
    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void Read_uses_sub_name_and_email() =>
        Assert.Equal(new KeycloakUser("abc", "Anna Peeters", "anna@org.be"),
            KeycloakClaims.Read(User(("sub", "abc"), ("name", "Anna Peeters"), ("email", "anna@org.be"))));

    [Fact]
    public void Read_falls_back_to_preferred_username() =>
        Assert.Equal("apeeters", KeycloakClaims.Read(User(("sub", "abc"), ("preferred_username", "apeeters")))!.Name);

    [Fact]
    public void Read_without_email_gives_null_email() =>
        Assert.Null(KeycloakClaims.Read(User(("sub", "abc"), ("name", "Anna")))!.Email);

    [Fact]
    public void Read_without_sub_returns_null() =>
        Assert.Null(KeycloakClaims.Read(User(("name", "Anna"), ("email", "anna@org.be"))));

    [Fact]
    public void Read_with_neither_name_nor_username_uses_sub() =>
        Assert.Equal("abc", KeycloakClaims.Read(User(("sub", "abc")))!.Name);
}
