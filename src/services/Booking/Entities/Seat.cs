using CommunityToolkit.Diagnostics;
using Contracts;
using Contracts.Exceptions;

namespace Booking.Entities;

public class Seat
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid SectionId { get; private set; }
    public int Row { get; private set; }
    public int Number { get; private set; }
    public Money Price { get; private set; }
    public string Category { get; private set; }
    public SeatStatuses Status { get; private set; }
    public uint RowVersion { get; private set; }

    private Seat()
    {
    }

    public Seat(
        Guid id,
        Guid eventId,
        Guid sectionId,
        int row,
        int number,
        Money price,
        string category)
    {
        Guard.IsNotNullOrWhiteSpace(category);

        Id = id;
        EventId = eventId;
        SectionId = sectionId;
        Row = row;
        Number = number;
        Price = price;
        Category = category;
        Status = SeatStatuses.Available;
    }

    public void Hold()
    {
        if (Status != SeatStatuses.Available)
            throw new DomainException("Seat is not available");

        Status = SeatStatuses.Held;
    }

    public void Sell()
    {
        if (Status != SeatStatuses.Held)
            throw new DomainException("Seat must be held before it can be sold");

        Status = SeatStatuses.Sold;
    }

    public void Release()
    {
        if (Status != SeatStatuses.Held)
            throw new DomainException("Only a held seat can be released");

        Status = SeatStatuses.Available;
    }
}
