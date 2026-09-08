using System.ComponentModel.DataAnnotations;
using Catalog.Entities;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Events;

public static class CreateEventHandler
{
    public static async Task<Results<Ok<Guid>, ProblemHttpResult>> HandleAsync(
        CreateEventRequest request,
        CatalogDbContext context,
        CancellationToken cancellationToken)
    {
        var isVenueExist = await context.Venues.AnyAsync(
            venue => venue.Id == request.VenueId,
            cancellationToken);
        if (!isVenueExist)
            return TypedResults.Problem(
                title: "VENUE_NOT_FOUND",
                detail: $"Venue with id {request.VenueId} not found.",
                statusCode: StatusCodes.Status400BadRequest);
        
        var @event = new Event(request.Title, request.Description, request.VenueId,
            request.StartsAt, request.SalesStartAt);
        await context.Events.AddAsync(@event, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        
        return TypedResults.Ok(@event.Id);
    }
}

public record CreateEventRequest(
    [Required] string Title,
    [Required] string  Description, 
    [Required] Guid VenueId,
    DateTime StartsAt, 
    DateTime SalesStartAt);
