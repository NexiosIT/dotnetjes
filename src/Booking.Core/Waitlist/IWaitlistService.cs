namespace Booking.Core.Waitlist;

public enum WaitlistError
{
    NotFound,
    OutOfRange,
    Weekend,
    PersonInactive,
    NoEmail,
    AlreadyHasCharger,
    ChargerSpotFree
}

public sealed record WaitlistResult(bool Success, WaitlistError? Error)
{
    public static WaitlistResult Ok() => new(true, null);
    public static WaitlistResult Fail(WaitlistError error) => new(false, error);
}

/// <summary>The per-day waitlist for charger spots. Waiting people are emailed when one frees up.</summary>
public interface IWaitlistService
{
    /// <summary>Joins the waitlist for <paramref name="date"/>. Already on it → success, nothing changes.</summary>
    Task<WaitlistResult> JoinAsync(int personId, DateOnly date, CancellationToken ct = default);

    /// <summary>Leaves the waitlist for <paramref name="date"/>. Not on it → nothing happens.</summary>
    Task LeaveAsync(int personId, DateOnly date, CancellationToken ct = default);

    Task<bool> IsOnWaitlistAsync(int personId, DateOnly date, CancellationToken ct = default);

    /// <summary>The waitlist emails people, so it needs an address (it comes from Keycloak).</summary>
    Task<bool> HasEmailAsync(int personId, CancellationToken ct = default);
}
