using Booking.Dtos;
using Booking.Entities;
using Booking.Infrastructure;
using Contracts.Events;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Bookings;

public static class ReleaseReservationHandler
{
    public static async Task<Results<Ok<ReservationDto>, ProblemHttpResult>> HandleAsync(
        Guid id,
        BookingDbContext context,
        CancellationToken cancellationToken)
    {
        var reservation = await ReleaseAsync(context, id, BookingReleaseReasons.UserCancelled, cancellationToken);
        if (reservation is null)
            return TypedResults.Problem(
                title: "RESERVATION_NOT_FOUND",
                detail: $"Reservation with id {id} not found.",
                statusCode: StatusCodes.Status404NotFound);

        await context.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ReservationDto.Create(reservation));
    }

    /// <summary>
    /// Без SaveChanges — см. ConfirmReservationHandler.ConfirmAsync.
    /// </summary>
    internal static async Task<Reservation?> ReleaseAsync(
        BookingDbContext context,
        Guid id,
        BookingReleaseReasons reason,
        CancellationToken cancellationToken)
    {
        var reservation = await context.Reservations
            .FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);
        if (reservation is null)
            return null;

        reservation.Release();
        await ConfirmReservationHandler.ReleaseSeatsAsync(context, reservation, reason, cancellationToken);

        return reservation;
    }
}
