namespace Contracts.Events;

public record OrderCompleted(
    Guid OrderId,
    Guid ReservationId,
    Guid EventId,
    IReadOnlyList<Guid> SeatIds);

public record OrderFailed(
    Guid OrderId,
    Guid ReservationId,
    OrderFailureReasons Reason);

public enum OrderFailureReasons
{
    ReservationExpired,
    PaymentFailed
}
