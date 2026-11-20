namespace Booking.Application.Waitlist;

/// <summary>
/// The charger waitlist. Rules about the person and the date come from <see cref="WaitlistEntry.Join"/>; whether the
/// person already holds a charger spot and whether one is still free depend on the whole day and are checked here.
/// </summary>
public sealed class WaitlistService(
    IUnitOfWorkFactory unitsOfWork,
    IOfficeCalendar calendar,
    TimeProvider time) : IWaitlistService
{
    /// <summary>Check order: NotFound → OutOfRange → Weekend → PersonInactive → NoEmail → AlreadyHasCharger → ChargerSpotFree.</summary>
    public async Task<WaitlistResult> JoinAsync(int personId, DateOnly date, CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);

        var person = await uow.People.GetAsync(personId, ct);
        if (person is null)
        {
            return WaitlistResult.Fail(WaitlistError.NotFound);
        }
        var joined = WaitlistEntry.Join(person, date, calendar, time.GetUtcNow().UtcDateTime);
        if (joined.Error is { } refused)
        {
            return WaitlistResult.Fail(refused);
        }
        if (await uow.Bookings.HasChargerSpotAsync(personId, date, ct))
        {
            return WaitlistResult.Fail(WaitlistError.AlreadyHasCharger);
        }
        if (await uow.Resources.AnyFreeChargerSpotAsync(date, ct))
        {
            return WaitlistResult.Fail(WaitlistError.ChargerSpotFree);
        }
        if (await uow.Waitlist.ExistsAsync(personId, date, ct))
        {
            return WaitlistResult.Ok();
        }

        uow.Waitlist.Add(joined.Value!);
        return await uow.SaveChangesAsync(ct) is null or SaveConflict.AlreadyOnWaitlist
            ? WaitlistResult.Ok()
            : throw new InvalidOperationException("Unexpected conflict while joining the waitlist.");
    }

    public async Task LeaveAsync(int personId, DateOnly date, CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);
        await uow.Waitlist.RemoveAsync(personId, date, ct);
    }

    public async Task<bool> IsOnWaitlistAsync(int personId, DateOnly date, CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);
        return await uow.Waitlist.ExistsAsync(personId, date, ct);
    }

    public async Task<bool> HasEmailAsync(int personId, CancellationToken ct = default)
    {
        await using var uow = await unitsOfWork.CreateAsync(ct);
        return (await uow.People.GetAsync(personId, ct))?.HasEmail ?? false;
    }
}
