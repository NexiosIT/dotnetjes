namespace Booking.Domain.Tests;

public class WaitlistEntryTests
{
    private static Result<WaitlistEntry, WaitlistError> Join(Person person, DateOnly date) =>
        WaitlistEntry.Join(person, date, Fixtures.Calendar, Fixtures.Now);

    [Fact]
    public void GivenABookableDay_WhenJoiningTheWaitlist_ThenAnEntryForThatDayIsCreated()
    {
        var result = Join(Fixtures.Anna(), Fixtures.Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(Fixtures.Today, result.Value!.Date);
        Assert.Equal(Fixtures.Now, result.Value.CreatedAt);
    }

    [Fact]
    public void GivenAnInactivePersonWithoutEmail_WhenJoining_ThenRangeWeekendActiveEmailAreCheckedInOrder()
    {
        var anna = Fixtures.Anna(email: null);
        anna.SetActive(false);

        Assert.Equal(WaitlistError.OutOfRange, Join(anna, Fixtures.Today.AddDays(-1)).Error);
        Assert.Equal(WaitlistError.Weekend, Join(anna, Fixtures.Saturday).Error);
        Assert.Equal(WaitlistError.PersonInactive, Join(anna, Fixtures.Today).Error);
        anna.SetActive(true);
        Assert.Equal(WaitlistError.NoEmail, Join(anna, Fixtures.Today).Error);
    }
}
