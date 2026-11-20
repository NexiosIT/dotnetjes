namespace Booking.Domain.Resources;

/// <summary>
/// A bookable desk or parking spot (aggregate root). Codes are trimmed and upper-cased, names trimmed.
/// Never deleted: deactivating hides it for new bookings while existing bookings stay valid.
/// </summary>
public class Resource
{
    private Resource()
    {
    }

    public int Id { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public ResourceType Type { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>Parking only; null for desks or when not set yet.</summary>
    public ParkingLocation? Location { get; private set; }

    /// <summary>Parking only: the spot has an EV charge port.</summary>
    public bool HasCharger { get; private set; }

    /// <summary>The spots the charger waitlist is about: active parking spots with a charger.</summary>
    public bool IsChargerSpot => Type == ResourceType.Parking && HasCharger && IsActive;

    public static Resource Create(string code, string name, ResourceType type) =>
        new() { Code = NormalizeCode(code), Name = DomainValidationException.Required(name, "Name"), Type = type };

    public void Update(string code, string name, bool isActive)
    {
        var normalizedCode = NormalizeCode(code);
        var trimmedName = DomainValidationException.Required(name, "Name");
        Code = normalizedCode;
        Name = trimmedName;
        IsActive = isActive;
    }

    /// <summary>Where a parking spot is and whether it has a charger. Desks can't have either.</summary>
    public void SetParkingDetails(ParkingLocation? location, bool hasCharger)
    {
        if (Type != ResourceType.Parking && (location is not null || hasCharger))
        {
            throw new DomainValidationException("Only parking spots have a location or charger.");
        }
        Location = location;
        HasCharger = hasCharger;
    }

    public static string NormalizeCode(string? code) => DomainValidationException.Required(code, "Code").ToUpperInvariant();
}
