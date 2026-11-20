using System.Reflection;
using Booking.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Booking.Web.Tests.Auth;

public class AdminPagesTests
{
    /// <summary>Every routable page under /admin, found by route so a newly added admin page is covered too.</summary>
    public static TheoryData<Type> AdminPages() =>
        new(typeof(Booking.Web.Components.App).Assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<RouteAttribute>().Any(r => r.Template.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))));

    [Fact]
    public void There_are_admin_pages() =>
        Assert.True(AdminPages().Count >= 3);

    [Theory]
    [MemberData(nameof(AdminPages))]
    public void Admin_pages_require_admin_policy(Type page) =>
        Assert.Equal(AdminAccess.Policy, page.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
}
