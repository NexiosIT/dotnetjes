using BookingEntity = Booking.Domain.Bookings.Booking;

namespace Booking.Domain.Tests;

public class BookingTests
{
    private static Result<BookingEntity, BookingError> Book(Person person, Resource resource, DateOnly date) =>
        BookingEntity.Book(person, resource, date, Fixtures.Calendar, Fixtures.Now);

    [Fact]
    public void Book_creates_a_booking_for_that_day()
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
    public void Book_outside_the_window_is_OutOfRange(int days) =>
        Assert.Equal(BookingError.OutOfRange, Book(Fixtures.Anna(), Fixtures.Desk(), Fixtures.Today.AddDays(days)).Error);

    [Fact]
    public void Book_on_a_weekend_is_refused() =>
        Assert.Equal(BookingError.Weekend, Book(Fixtures.Anna(), Fixtures.Desk(), Fixtures.Saturday).Error);

    [Fact]
    public void Book_checks_person_before_resource()
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
    public void Range_is_checked_before_weekend_and_people() =>
        Assert.Equal(BookingError.OutOfRange, Book(Fixtures.Anna(), Fixtures.Desk(), new DateOnly(2026, 12, 5)).Error);

    [Fact]
    public void Only_the_owner_can_cancel_and_not_in_the_past()
    {
        var booking = Book(Fixtures.Anna(), Fixtures.Desk(), Fixtures.Today.AddDays(1)).Value!;

        Assert.Equal(BookingError.NotYourBooking, booking.CanBeCancelledBy(booking.PersonId + 1, Fixtures.Today));
        Assert.Null(booking.CanBeCancelledBy(booking.PersonId, Fixtures.Today));
        Assert.Equal(BookingError.OutOfRange, booking.CanBeCancelledBy(booking.PersonId, Fixtures.Today.AddDays(2)));
    }
}
