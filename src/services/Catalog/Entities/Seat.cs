using Contracts;

namespace Catalog.Entities;

public class Seat
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid SectionId { get; private set; }
    public int Row { get; private set; }
    public int Number { get; private set; }
    public Money Price { get; private set; }
    public SeatStatuses Status { get; private set; }

    private Seat()
    {
    }

    public Seat(
        Guid eventId,
        Guid sectionId,
        int row,
        int number,
        Money price)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        SectionId = sectionId;
        Row = row;
        Number = number;
        Price = price;
        Status = SeatStatuses.Available;
    }
}
