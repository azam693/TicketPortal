namespace Contracts.Events;

public record PaymentProcessed(Guid OrderId, PaymentOutcomes Outcome);

public enum PaymentOutcomes
{
    Succeeded,
    Failed
}
