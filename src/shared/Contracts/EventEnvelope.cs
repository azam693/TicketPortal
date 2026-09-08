namespace Contracts;

public record EventEnvelope<T>(
    Guid MessageId,
    Guid CorrelationId,
    Guid CausationId,
    DateTimeOffset OccurredAt,
    T Payload
);
