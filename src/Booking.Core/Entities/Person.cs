namespace Booking.Core.Entities;

public class Person
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>The Keycloak user id (<c>sub</c> claim). Null for people who never logged in.</summary>
    public string? ExternalId { get; set; }
}
