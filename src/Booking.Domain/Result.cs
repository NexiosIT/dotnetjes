namespace Booking.Domain;

/// <summary>Either a value or the reason a domain rule refused it. Refusals are expected outcomes, not exceptions.</summary>
public readonly record struct Result<T, TError>(T? Value, TError? Error)
    where T : class
    where TError : struct, Enum
{
    public bool IsSuccess => Error is null;

    public static Result<T, TError> Ok(T value) => new(value, null);

    public static Result<T, TError> Fail(TError error) => new(null, error);
}
