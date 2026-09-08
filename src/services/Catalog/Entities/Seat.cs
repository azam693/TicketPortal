using Contracts;

namespace Catalog.Entities;

public class Seat
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public Guid SectionId { get; init; }
    public int Row { get; init; }
    public int Number { get; init; }
    public Money Price { get; init; }
    public SeatStatuses Status { get; set; }
}
