using Booking.Entities;

namespace Booking.Dtos;

public record SeatDto(
    Guid Id,
    Guid SectionId,
    int Row,
    int Number,
    decimal Price,
    string Currency,
    string Category,
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
            seat.Category,
            seat.Status.ToString());
}
