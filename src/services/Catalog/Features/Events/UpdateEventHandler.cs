using System.ComponentModel.DataAnnotations;
using Contracts.Exceptions;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.Features.Events;

public static class UpdateEventHandler
{
    public static async Task<Results<Ok, ProblemHttpResult>> HandleAsync(
        Guid id,
        UpdateEventRequest request,
        CatalogDbContext context,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        var @event = await context.Events
            .FirstOrDefaultAsync(@event => @event.Id == id, cancellationToken);
        if (@event is null)
            return TypedResults.Problem(
                title: "EVENT_NOT_FOUND",
                detail: $"Event with id {id} not found.",
                statusCode: StatusCodes.Status404NotFound);

        try
        {
            @event.Update(request.Title, request.Description, request.StartsAt, request.SalesStartAt);
        }
        catch (DomainException exception)
        {
            return TypedResults.Problem(
                title: "EVENT_INVALID",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync($"event:{id}", cancellationToken);

        return TypedResults.Ok();
    }
}

public record UpdateEventRequest(
    [Required] string Title,
    [Required] string Description,
    DateTime StartsAt,
    DateTime SalesStartAt);
