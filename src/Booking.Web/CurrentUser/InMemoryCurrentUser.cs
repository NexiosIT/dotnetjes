namespace Booking.Web.CurrentUser;

/// <summary>Forgets the choice on page reload. Replaced by LocalStorageCurrentUser in task B1.</summary>
public sealed class InMemoryCurrentUser : ICurrentUser
{
    private int? _personId;

    public event Action? Changed;

    public Task<int?> GetPersonIdAsync() => Task.FromResult(_personId);

    public Task SetAsync(int personId)
    {
        _personId = personId;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _personId = null;
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
