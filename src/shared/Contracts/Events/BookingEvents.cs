namespace Contracts.Events;

public record SeatsHeld(
    Guid ReservationId,
    Guid EventId,
    IReadOnlyList<Guid> SeatIds,
    DateTimeOffset ExpiresAt);

public record BookingConfirmed(
    Guid ReservationId,
    Guid EventId,
    IReadOnlyList<Guid> SeatIds);

public record BookingReleased(
    Guid ReservationId,
    Guid EventId,
    IReadOnlyList<Guid> SeatIds,
    BookingReleaseReasons Reason);

public enum BookingReleaseReasons
{
    UserCancelled,
    Expired,
    PaymentFailed
}
