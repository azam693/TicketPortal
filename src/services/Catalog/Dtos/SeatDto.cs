using Catalog.Entities;

namespace Catalog.Dtos;

public record SeatDto(
    Guid Id,
    Guid SectionId,
    int Row,
    int Number,
    decimal Price,
    string Currency,
    string Status)
{
    public static SeatDto Create(Seat seat) =>
        new(
            seat.Id,
            seat.SectionId,
            seat.Row,
            seat.Number,
            seat.Price.Amount,
            seat.Price.Currency,
            seat.Status.ToString());
}
