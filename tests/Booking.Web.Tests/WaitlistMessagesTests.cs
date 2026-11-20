using Booking.Application.Waitlist;
using Booking.Web.Components.Pages;

namespace Booking.Web.Tests;

public class WaitlistMessagesTests
{
    [Fact]
    public void WhenGettingTheMessageForEveryWaitlistError_ThenNoneIsBlank() =>
        Assert.All(Enum.GetValues<WaitlistError>(), e => Assert.False(string.IsNullOrWhiteSpace(WaitlistMessages.For(e))));

    [Fact]
    public void GivenTheNoEmailError_WhenGettingItsMessage_ThenItMentionsTheCompanyAccount() =>
        Assert.Contains("company account", WaitlistMessages.For(WaitlistError.NoEmail));
}
