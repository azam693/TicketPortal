using Booking.Entities;
using Booking.Infrastructure;
using Contracts;
using Contracts.Events;
using MassTransit;
using Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Integration;

public class EventPublishedConsumer(BookingDbContext dbContext) : IConsumer<EventPublished>
{
    public async Task Consume(ConsumeContext<EventPublished> context)
    {
        var messageId = context.MessageId ?? throw new InvalidOperationException(
            "EventPublished message is missing a transport MessageId, required for inbox deduplication.");

        var alreadyProcessed = await dbContext.InboxMessages
            .AnyAsync(message => message.Id == messageId, context.CancellationToken);
        if (alreadyProcessed)
            return;

        var payload = context.Message;

        var existingSeatIds = await dbContext.Seats
            .Where(seat => seat.EventId == payload.EventId)
            .Select(seat => seat.Id)
            .ToListAsync(context.CancellationToken);

        var newSeats = payload.Seats
            .Where(snapshot => !existingSeatIds.Contains(snapshot.SeatId))
            .Select(snapshot => new Seat(
                snapshot.SeatId,
                payload.EventId,
                snapshot.SectionId,
                snapshot.Row,
                snapshot.Number,
                new Money(snapshot.Price, snapshot.Currency),
                snapshot.Category))
            .ToList();

        await dbContext.Seats.AddRangeAsync(newSeats, context.CancellationToken);
        await dbContext.InboxMessages.AddAsync(new InboxMessage(messageId), context.CancellationToken);

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
