namespace Booking.Domain;

/// <summary>Invalid input for an admin action; the message is fit to show the user.</summary>
public sealed class DomainValidationException(string message) : Exception(message)
{
    /// <summary>The trimmed value, or a <see cref="DomainValidationException"/> "{field} is required." when it's blank.</summary>
    public static string Required(string? value, string field)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? throw new DomainValidationException($"{field} is required.") : trimmed;
    }
}
