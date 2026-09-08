namespace Catalog.Entities;

public class Venue
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string City { get; init; }
    public string Address { get; init; }
    public IReadOnlyList<SeatMapSection> Sections { get; init; }
}
