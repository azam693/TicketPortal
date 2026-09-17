using Contracts;
using Order.Entities;

namespace Order.Dtos;

public record OrderDto(
    Guid Id,
    Guid ReservationId,
    string Status,
    Guid? EventId,
    IReadOnlyList<Guid> SeatIds,
    Guid? CustomerId,
    Money Amount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt)
{
    public static OrderDto Create(OrderState state) => new(
        state.CorrelationId,
        state.ReservationId,
        state.CurrentState,
        state.EventId,
        state.SeatIds,
        state.CustomerId,
        state.Amount,
        state.CreatedAt,
        state.CompletedAt);
}
