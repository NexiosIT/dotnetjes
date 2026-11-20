using Microsoft.JSInterop;

namespace Booking.Web.CurrentUser;

/// <summary>Remembers the chosen person in the browser's localStorage so a reload keeps it.</summary>
public sealed class LocalStorageCurrentUser(IJSRuntime js) : ICurrentUser
{
    private const string Key = "booking.personId";

    private int? _personId;
    private bool _loaded;

    public event Action? Changed;

    public async Task<int?> GetPersonIdAsync()
    {
        if (!_loaded)
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            _personId = int.TryParse(stored, out var id) ? id : null;
            _loaded = true;
        }

        return _personId;
    }

    public async Task SetAsync(int personId)
    {
        await js.InvokeVoidAsync("localStorage.setItem", Key, personId.ToString());
        _personId = personId;
        _loaded = true;
        Changed?.Invoke();
    }

    public async Task ClearAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", Key);
        _personId = null;
        _loaded = true;
        Changed?.Invoke();
    }
}
