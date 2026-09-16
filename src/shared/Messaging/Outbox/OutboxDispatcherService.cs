using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging.Outbox;

public class OutboxDispatcherService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherService<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext, IOutboxDbContext
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DispatchPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox dispatch iteration failed");
            }
        }
    }

    private async Task DispatchPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await context.OutboxMessages
            .Where(message => message.ProcessedAt == null)
            .OrderBy(message => message.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            try
            {
                var type = Type.GetType(message.Type)
                           ?? throw new InvalidOperationException($"Unknown outbox message type '{message.Type}'.");

                var payload = JsonSerializer.Deserialize(message.Content, type)
                              ?? throw new InvalidOperationException($"Failed to deserialize outbox message {message.Id}.");

                // MessageId = Id этой outbox-записи: если диспетчер упадёт
                // после Publish, но до MarkProcessed, повторная отправка при
                // рестарте придёт с тем же MessageId — потребитель дедупит
                // по нему через Inbox.
                await publishEndpoint.Publish(payload, type, ctx => ctx.MessageId = message.Id, cancellationToken);

                message.MarkProcessed();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {MessageId}", message.Id);
                message.MarkFailed(ex.Message);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
