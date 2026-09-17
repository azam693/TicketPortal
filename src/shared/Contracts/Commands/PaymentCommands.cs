namespace Contracts.Commands;

public record ProcessPayment(Guid OrderId, Money Amount);
