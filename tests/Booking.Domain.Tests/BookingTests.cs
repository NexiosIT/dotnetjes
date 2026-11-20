using BookingEntity = Booking.Domain.Bookings.Booking;

namespace Booking.Domain.Tests;

public class BookingTests
{
    private static Result<BookingEntity, BookingError> Book(Person person, Resource resource, DateOnly date) =>
        BookingEntity.Book(person, resource, date, Fixtures.Calendar, Fixtures.Now);

    [Fact]
    public void GivenABookableDay_WhenBookingAResource_ThenABookingForThatDayIsCreated()
    {
        var result = Book(Fixtures.Anna(), Fixtures.ChargerSpot(), Fixtures.Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(Fixtures.Today, result.Value!.Date);
        Assert.Equal(ResourceType.Parking, result.Value.ResourceType);
        Assert.Equal(Fixtures.Now, result.Value.CreatedAt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(15)]
    public void GivenADayOutsideTheBookingWindow_WhenBooking_ThenTheErrorIsOutOfRange(int days) =>
        Assert.Equal(BookingError.OutOfRange, Book(Fixtures.Anna(), Fixtures.Desk(), Fixtures.Today.AddDays(days)).Error);

    [Fact]
    public void GivenAWeekendDay_WhenBooking_ThenTheErrorIsWeekend() =>
        Assert.Equal(BookingError.Weekend, Book(Fixtures.Anna(), Fixtures.Desk(), Fixtures.Saturday).Error);

    [Fact]
    public void GivenBothInactive_WhenBooking_ThenPersonInactiveComesBeforeResourceInactive()
    {
        var anna = Fixtures.Anna();
        anna.SetActive(false);
        var desk = Fixtures.Desk();
        desk.Update(desk.Code, desk.Name, isActive: false);

        Assert.Equal(BookingError.PersonInactive, Book(anna, desk, Fixtures.Today).Error);
        anna.SetActive(true);
        Assert.Equal(BookingError.ResourceInactive, Book(anna, desk, Fixtures.Today).Error);
    }

    [Fact]
    public void GivenAWeekendDayOutOfRange_WhenBooking_ThenOutOfRangeIsReportedFirst() =>
        Assert.Equal(BookingError.OutOfRange, Book(Fixtures.Anna(), Fixtures.Desk(), new DateOnly(2026, 12, 5)).Error);

    [Fact]
    public void GivenABooking_WhenCancelling_ThenOnlyTheOwnerMayAndNotAfterTheDayHasPassed()
    {
        var booking = Book(Fixtures.Anna(), Fixtures.Desk(), Fixtures.Today.AddDays(1)).Value!;

        Assert.Equal(BookingError.NotYourBooking, booking.CanBeCancelledBy(booking.PersonId + 1, Fixtures.Today));
        Assert.Null(booking.CanBeCancelledBy(booking.PersonId, Fixtures.Today));
        Assert.Equal(BookingError.OutOfRange, booking.CanBeCancelledBy(booking.PersonId, Fixtures.Today.AddDays(2)));
    }
}
