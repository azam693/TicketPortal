using System.Text.Json;

namespace Booking.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Content { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage()
    {
    }

    public OutboxMessage(object data)
    {
        Id = Guid.NewGuid();
        Type = data.GetType().FullName!;
        Content = JsonSerializer.Serialize(data);
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;
    public void MarkFailed(string error) => Error = error;
}
