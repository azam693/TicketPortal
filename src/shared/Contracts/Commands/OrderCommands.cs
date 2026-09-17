namespace Contracts.Commands;

public record SubmitOrder(
    Guid ReservationId,
    Guid? CustomerId,
    decimal Amount,
    string Currency);
