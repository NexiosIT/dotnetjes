namespace Booking.Domain.Tests;

public class PersonTests
{
    [Fact]
    public void FromLogin_trims_and_stores_blank_email_as_null()
    {
        var person = Person.FromLogin("kc-1", " Anna ", "  ");

        Assert.Equal("kc-1", person.ExternalId);
        Assert.Equal("Anna", person.Name);
        Assert.Null(person.Email);
        Assert.True(person.IsActive);
        Assert.False(person.HasEmail);
    }

    [Fact]
    public void RefreshProfile_updates_name_and_email_but_not_active()
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
