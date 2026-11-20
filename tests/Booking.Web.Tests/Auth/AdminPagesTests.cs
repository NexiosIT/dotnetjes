using System.Reflection;
using Booking.Web.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Booking.Web.Tests.Auth;

public class AdminPagesTests
{
    [Theory]
    [InlineData(typeof(Booking.Web.Components.Pages.Admin.Resources))]
    [InlineData(typeof(Booking.Web.Components.Pages.Admin.People))]
    public void Admin_pages_require_admin_policy(Type page) =>
        Assert.Equal(AdminAccess.Policy, page.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
}
