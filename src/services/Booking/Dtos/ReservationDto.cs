using Booking.Entities;

namespace Booking.Dtos;

public record ReservationDto(
    Guid Id,
    Guid EventId,
    Guid? CustomerId,
    string Status,
    IReadOnlyList<Guid> SeatIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConfirmedAt)
{
    public static ReservationDto Create(Reservation reservation) =>
        new(
            reservation.Id,
            reservation.EventId,
            reservation.CustomerId,
            reservation.Status.ToString(),
            reservation.Seats.Select(seat => seat.SeatId).ToList(),
            reservation.CreatedAt,
            reservation.ExpiresAt,
            reservation.ConfirmedAt);
}
