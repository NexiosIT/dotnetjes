using Booking.Web.Auth;

namespace Booking.Web.Tests.Auth;

public class KeycloakRolesTests
{
    private static string Token(string payload) => TestTokens.WithPayload(payload);

    [Fact]
    public void Reads_roles_of_this_client() =>
        Assert.Equal(["Admin", "other"], KeycloakRoles.ClientRoles(
            Token("""{"resource_access":{"booking":{"roles":["Admin","other"]}}}"""), "booking"));

    [Fact]
    public void Ignores_roles_of_other_clients() =>
        Assert.Empty(KeycloakRoles.ClientRoles(
            Token("""{"resource_access":{"account":{"roles":["Admin"]}}}"""), "booking"));

    [Fact]
    public void Ignores_realm_roles() =>
        Assert.Empty(KeycloakRoles.ClientRoles(
            Token("""{"realm_access":{"roles":["Admin"]}}"""), "booking"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.!!!.c")]
    [InlineData("a.bm90LWpzb24.c")] // "not-json"
    public void Malformed_or_missing_token_gives_no_roles(string? token) =>
        Assert.Empty(KeycloakRoles.ClientRoles(token, "booking"));

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"resource_access":"oops"}""")]
    [InlineData("""{"resource_access":{"booking":{}}}""")]
    [InlineData("""{"resource_access":{"booking":{"roles":"Admin"}}}""")]
    [InlineData("""{"resource_access":{"booking":{"roles":[1,null,"Admin"]}}}""")]
    public void Unexpected_shapes_give_only_string_roles(string payload) =>
        Assert.DoesNotContain(KeycloakRoles.ClientRoles(Token(payload), "booking"), r => r != "Admin");
}
