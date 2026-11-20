using Booking.Application.Waitlist;
using Booking.Web.Components.Pages;

namespace Booking.Web.Tests;

public class WaitlistMessagesTests
{
    [Fact]
    public void Every_error_has_a_message() =>
        Assert.All(Enum.GetValues<WaitlistError>(), e => Assert.False(string.IsNullOrWhiteSpace(WaitlistMessages.For(e))));

    [Fact]
    public void NoEmail_points_to_company_account() =>
        Assert.Contains("company account", WaitlistMessages.For(WaitlistError.NoEmail));
}
