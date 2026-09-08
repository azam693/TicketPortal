namespace Catalog.Entities;

public class SeatMapSection
{
    public Guid Id { get; init; }
    public string Name { get; init; }        // "Партер", "Балкон А"
    public int RowsCount { get; init; }
    public int SeatsPerRow { get; init; }
    public SeatCategories Category { get; init; }
}
