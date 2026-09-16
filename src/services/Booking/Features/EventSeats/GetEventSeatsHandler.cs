using Booking.Dtos;
using Booking.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.EventSeats;

public static class GetEventSeatsHandler
{
    public static async Task<Ok<IReadOnlyList<SeatDto>>> HandleAsync(
        Guid eventId,
        BookingDbContext context,
        CancellationToken cancellationToken)
    {
        var seats = await context.Seats
            .AsNoTracking()
            .Where(seat => seat.EventId == eventId)
            .OrderBy(seat => seat.SectionId)
            .ThenBy(seat => seat.Row)
            .ThenBy(seat => seat.Number)
            .ToListAsync(cancellationToken);

        var seatDtos = seats.Select(SeatDto.Create).ToList();

        return TypedResults.Ok<IReadOnlyList<SeatDto>>(seatDtos);
    }
}
