using Microsoft.EntityFrameworkCore;

namespace Messaging.Outbox;

/// <summary>
/// Реализуется DbContext сервиса, чтобы generic OutboxDispatcherService
/// мог читать и помечать его outbox-таблицу без знания о конкретном сервисе.
/// </summary>
public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
}
