namespace Booking.Domain.Tests;

public class PersonTests
{
    [Fact]
    public void WhenCreatingAPersonFromALogin_ThenValuesAreTrimmedAndABlankEmailBecomesNull()
    {
        var person = Person.FromLogin("kc-1", " Anna ", "  ");

        Assert.Equal("kc-1", person.ExternalId);
        Assert.Equal("Anna", person.Name);
        Assert.Null(person.Email);
        Assert.True(person.IsActive);
        Assert.False(person.HasEmail);
    }

    [Fact]
    public void GivenAnInactivePerson_WhenRefreshingProfile_ThenNameAndEmailChangeAndActiveStays()
    {
        var person = Fixtures.Anna();
        person.SetActive(false);

        person.RefreshProfile(" Anna P. ", " anna.p@example.com ");

        Assert.Equal("Anna P.", person.Name);
        Assert.Equal("anna.p@example.com", person.Email);
        Assert.True(person.HasEmail);
        Assert.False(person.IsActive);
    }
}
