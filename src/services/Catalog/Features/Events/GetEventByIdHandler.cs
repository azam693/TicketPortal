using System.Text.Json;
using Catalog.Dtos;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.Features.Events;

public static class GetEventByIdHandler
{    
    public static async Task<Results<Ok<EventDto>, ProblemHttpResult>> HandleAsync(
        Guid id,
        CatalogDbContext context,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"event:{id}";
        var cached = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
            return TypedResults.Ok(JsonSerializer.Deserialize<EventDto>(cached));

        var @event = await context.Events
            .FirstOrDefaultAsync(@event => @event.Id == id, cancellationToken);
        if (@event is null)
            return TypedResults.Problem(
                title: "EVENT_NOT_FOUND",
                detail: $"Event with id {id} not found.",
                statusCode: StatusCodes.Status404NotFound);

        var eventDto = EventDto.Create(@event);
        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(eventDto),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)},
            cancellationToken);
        
        return TypedResults.Ok(eventDto);
    }
}
