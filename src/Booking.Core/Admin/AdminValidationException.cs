namespace Booking.Core.Admin;

public sealed class AdminValidationException(string message) : Exception(message);
