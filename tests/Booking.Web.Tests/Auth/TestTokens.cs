using System.Text;

namespace Booking.Web.Tests.Auth;

/// <summary>Unsigned JWTs for tests: only the payload matters.</summary>
public static class TestTokens
{
    public static string WithPayload(string json) =>
        "e30." + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".sig";
}
