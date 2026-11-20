using Booking.Web.Auth;

namespace Booking.Web.Tests.Auth;

public class KeycloakOptionsTests
{
    [Fact]
    public void GivenAuthorityClientIdAndSecretAreSet_WhenCheckingIsComplete_ThenItIsTrue() =>
        Assert.True(new KeycloakOptions { Authority = "https://sso/realms/x", ClientId = "app", ClientSecret = "s3cret" }.IsComplete);

    [Theory]
    [InlineData("", "app", "s")]
    [InlineData("https://sso", " ", "s")]
    [InlineData("https://sso", "app", "")]
    public void GivenTheAuthorityClientIdOrSecretIsBlank_WhenCheckingIsComplete_ThenItIsFalse(string authority, string clientId, string secret) =>
        Assert.False(new KeycloakOptions { Authority = authority, ClientId = clientId, ClientSecret = secret }.IsComplete);
}
