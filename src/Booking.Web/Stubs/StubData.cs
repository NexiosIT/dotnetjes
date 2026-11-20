using Booking.Core.Entities;

namespace Booking.Web.Stubs;

/// <summary>Fixed fake data shared by the stubs until the real services are merged.</summary>
public static class StubData
{
    public static IReadOnlyList<Person> People { get; } =
    [
        new() { Id = 1, Name = "Anna", Email = "anna@example.com" },
        new() { Id = 2, Name = "Ben", Email = "ben@example.com" },
        new() { Id = 3, Name = "Chris", Email = "chris@example.com" },
    ];

    public static IReadOnlyList<Resource> Resources { get; } =
    [
        new() { Id = 1, Code = "D1", Name = "Desk 1", Type = ResourceType.Desk },
        new() { Id = 2, Code = "D2", Name = "Desk 2", Type = ResourceType.Desk },
        new() { Id = 3, Code = "D3", Name = "Desk 3", Type = ResourceType.Desk },
        new() { Id = 4, Code = "P1", Name = "Parking 1", Type = ResourceType.Parking },
        new() { Id = 5, Code = "P2", Name = "Parking 2", Type = ResourceType.Parking },
    ];
}
