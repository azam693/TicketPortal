using CommunityToolkit.Diagnostics;
using Contracts.Exceptions;

namespace Catalog.Entities;

public class SeatMapSection
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }        // "Партер", "Балкон А"
    public int RowsCount { get; private set; }
    public int SeatsPerRow { get; private set; }
    public SeatCategories Category { get; private set; }

    private SeatMapSection()
    {
    }

    public SeatMapSection(
        string name,
        int rowsCount,
        int seatsPerRow,
        SeatCategories category)
    {
        Guard.IsNotNullOrWhiteSpace(name);

        if (rowsCount <= 0)
            throw new DomainException("Section must have at least one row");
        if (seatsPerRow <= 0)
            throw new DomainException("Section must have at least one seat per row");

        Id = Guid.NewGuid();
        Name = name.Trim();
        RowsCount = rowsCount;
        SeatsPerRow = seatsPerRow;
        Category = category;
    }
}
