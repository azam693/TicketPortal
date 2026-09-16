namespace Booking.Entities;

/// <summary>
/// Дедупликация входящих интеграционных событий на стороне потребителя.
/// Id = EventEnvelope.MessageId издателя (at-least-once delivery от RabbitMQ).
/// </summary>
public class InboxMessage
{
    public Guid Id { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    private InboxMessage()
    {
    }

    public InboxMessage(Guid messageId)
    {
        Id = messageId;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
