namespace Contracts.Events;

public record EventPublished(
    Guid EventId,
    Guid VenueId,
    IReadOnlyList<SeatSnapshot> Seats);

public record SeatSnapshot(
    Guid SeatId,
    Guid SectionId,
    int Row,
    int Number,
    decimal Price,
    string Currency,
    string Category);
