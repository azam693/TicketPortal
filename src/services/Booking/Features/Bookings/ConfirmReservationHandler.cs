using Booking.Dtos;
using Booking.Entities;
using Booking.Infrastructure;
using Contracts.Events;
using Messaging.Outbox;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Bookings;

public static class ConfirmReservationHandler
{
    public static async Task<Results<Ok<ReservationDto>, ProblemHttpResult>> HandleAsync(
        Guid id,
        BookingDbContext context,
        CancellationToken cancellationToken)
    {
        var reservation = await context.Reservations
            .FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);
        if (reservation is null)
            return TypedResults.Problem(
                title: "RESERVATION_NOT_FOUND",
                detail: $"Reservation with id {id} not found.",
                statusCode: StatusCodes.Status404NotFound);

        if (reservation.Status == ReservationStatuses.Held && reservation.ExpiresAt < DateTimeOffset.UtcNow)
        {
            reservation.Expire();
            await ReleaseSeatsAsync(context, reservation, BookingReleaseReasons.Expired, cancellationToken);

            return TypedResults.Problem(
                title: "RESERVATION_EXPIRED",
                detail: "Hold expired before confirmation.",
                statusCode: StatusCodes.Status409Conflict);
        }

        reservation.Confirm();

        var seatIds = reservation.Seats.Select(seat => seat.SeatId).ToList();
        var seats = await context.Seats
            .Where(seat => seatIds.Contains(seat.Id))
            .ToListAsync(cancellationToken);

        foreach (var seat in seats)
            seat.Sell();

        context.OutboxMessages.Add(new OutboxMessage(
            new BookingConfirmed(reservation.Id, reservation.EventId, seatIds)));

        await context.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ReservationDto.Create(reservation));
    }

    internal static async Task ReleaseSeatsAsync(
        BookingDbContext context,
        Reservation reservation,
        BookingReleaseReasons reason,
        CancellationToken cancellationToken)
    {
        var seatIds = reservation.Seats.Select(seat => seat.SeatId).ToList();
        var seats = await context.Seats
            .Where(seat => seatIds.Contains(seat.Id))
            .ToListAsync(cancellationToken);

        foreach (var seat in seats)
        {
            seat.Release();
        }

        context.OutboxMessages.Add(new OutboxMessage(
            new BookingReleased(reservation.Id, reservation.EventId, seatIds, reason)));

        await context.SaveChangesAsync(cancellationToken);
    }
}
