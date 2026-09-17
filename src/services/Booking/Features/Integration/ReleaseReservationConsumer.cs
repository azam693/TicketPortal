using Booking.Features.Bookings;
using Booking.Infrastructure;
using Contracts.Commands;
using MassTransit;
using Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Integration;

public class ReleaseReservationConsumer(BookingDbContext dbContext) : IConsumer<ReleaseReservation>
{
    public async Task Consume(ConsumeContext<ReleaseReservation> context)
    {
        var messageId = context.MessageId ?? throw new InvalidOperationException(
            "ReleaseReservation message is missing a transport MessageId, required for inbox deduplication.");

        var alreadyProcessed = await dbContext.InboxMessages
            .AnyAsync(message => message.Id == messageId, context.CancellationToken);
        if (alreadyProcessed)
            return;

        await ReleaseReservationHandler.ReleaseAsync(
            dbContext, context.Message.ReservationId, context.Message.Reason, context.CancellationToken);

        dbContext.InboxMessages.Add(new InboxMessage(messageId));
        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
