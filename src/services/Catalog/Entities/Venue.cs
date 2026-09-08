using CommunityToolkit.Diagnostics;
using Contracts.Exceptions;

namespace Catalog.Entities;

public class Venue
{
    private readonly List<SeatMapSection> _sections = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string City { get; private set; }
    public string Address { get; private set; }
    public IReadOnlyList<SeatMapSection> Sections => _sections;

    private Venue()
    {
    }

    public Venue(
        string name,
        string city,
        string address,
        IEnumerable<SeatMapSection> sections)
    {
        Guard.IsNotNullOrWhiteSpace(name);
        Guard.IsNotNullOrWhiteSpace(city);
        Guard.IsNotNullOrWhiteSpace(address);

        var sectionList = sections?.ToList() ?? [];
        if (sectionList.Count == 0)
            throw new DomainException("Venue must have at least one section");

        Id = Guid.NewGuid();
        Name = name.Trim();
        City = city.Trim();
        Address = address.Trim();
        _sections = sectionList;
    }
}
