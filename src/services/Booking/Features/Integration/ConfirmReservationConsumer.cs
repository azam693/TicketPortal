using Booking.Features.Bookings;
using Booking.Infrastructure;
using Contracts.Commands;
using MassTransit;
using Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Integration;

public class ConfirmReservationConsumer(BookingDbContext dbContext) : IConsumer<ConfirmReservation>
{
    public async Task Consume(ConsumeContext<ConfirmReservation> context)
    {
        var messageId = context.MessageId ?? throw new InvalidOperationException(
            "ConfirmReservation message is missing a transport MessageId, required for inbox deduplication.");

        var alreadyProcessed = await dbContext.InboxMessages
            .AnyAsync(message => message.Id == messageId, context.CancellationToken);
        if (alreadyProcessed)
            return;

        await ConfirmReservationHandler.ConfirmAsync(dbContext, context.Message.ReservationId, context.CancellationToken);

        dbContext.InboxMessages.Add(new InboxMessage(messageId));
        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
