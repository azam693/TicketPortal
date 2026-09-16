using Booking.Dtos;
using Booking.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Bookings;

public static class GetReservationByIdHandler
{
    public static async Task<Results<Ok<ReservationDto>, ProblemHttpResult>> HandleAsync(
        Guid id,
        BookingDbContext context,
        CancellationToken cancellationToken)
    {
        var reservation = await context.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);
        if (reservation is null)
            return TypedResults.Problem(
                title: "RESERVATION_NOT_FOUND",
                detail: $"Reservation with id {id} not found.",
                statusCode: StatusCodes.Status404NotFound);

        return TypedResults.Ok(ReservationDto.Create(reservation));
    }
}
