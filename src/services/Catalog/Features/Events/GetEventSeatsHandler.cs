using Catalog.Dtos;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Events;

public static class GetEventSeatsHandler
{
    public static async Task<Results<Ok<IReadOnlyList<SeatDto>>, ProblemHttpResult>> HandleAsync(
        Guid id,
        CatalogDbContext context,
        CancellationToken cancellationToken)
    {
        var eventExists = await context.Events.AnyAsync(@event => @event.Id == id, cancellationToken);
        if (!eventExists)
            return TypedResults.Problem(
                title: "EVENT_NOT_FOUND",
                detail: $"Event with id {id} not found.",
                statusCode: StatusCodes.Status404NotFound);

        var seats = await context.Seats
            .AsNoTracking()
            .Where(seat => seat.EventId == id)
            .OrderBy(seat => seat.SectionId)
            .ThenBy(seat => seat.Row)
            .ThenBy(seat => seat.Number)
            .ToListAsync(cancellationToken);

        var seatDtos = seats.Select(SeatDto.Create).ToList();

        return TypedResults.Ok<IReadOnlyList<SeatDto>>(seatDtos);
    }
}
