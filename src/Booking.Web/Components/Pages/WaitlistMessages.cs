using Booking.Core.Waitlist;

namespace Booking.Web.Components.Pages;

/// <summary>Plain-language text shown above the day view when joining the charger waitlist fails.</summary>
public static class WaitlistMessages
{
    public static string For(WaitlistError error) => error switch
    {
        WaitlistError.NotFound => "That no longer exists — the page has been refreshed.",
        WaitlistError.OutOfRange => "That day can't be booked.",
        WaitlistError.Weekend => "The office is closed at weekends.",
        WaitlistError.PersonInactive => "Your profile is inactive — ask an admin.",
        WaitlistError.NoEmail => "The waitlist emails you, but your company account has no email address. Ask whoever manages accounts to add one.",
        WaitlistError.AlreadyHasCharger => "You already have a charger spot that day.",
        WaitlistError.ChargerSpotFree => "A charger spot is free — book it now.",
        _ => throw new ArgumentOutOfRangeException(nameof(error), error, null)
    };
}
