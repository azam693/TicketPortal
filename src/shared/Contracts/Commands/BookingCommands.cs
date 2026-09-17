using Contracts.Events;

namespace Contracts.Commands;

public record ConfirmReservation(Guid ReservationId);

public record ReleaseReservation(Guid ReservationId, BookingReleaseReasons Reason);
