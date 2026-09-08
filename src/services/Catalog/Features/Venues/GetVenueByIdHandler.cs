using System.Text.Json;
using Catalog.Dtos;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.Features.Venues;

public static class GetVenueByIdHandler
{
    public static async Task<Results<Ok<VenueDto>, ProblemHttpResult>> HandleAsync(
        Guid id,
        CatalogDbContext context,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"venue:{id}";
        var cached = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
            return TypedResults.Ok(JsonSerializer.Deserialize<VenueDto>(cached));

        var venue = await context.Venues
            .AsNoTracking()
            .Include(venue => venue.Sections)
            .FirstOrDefaultAsync(venue => venue.Id == id, cancellationToken);
        if (venue is null)
            return TypedResults.Problem(
                title: "VENUE_NOT_FOUND",
                detail: $"Venue with id {id} not found.",
                statusCode: StatusCodes.Status404NotFound);

        var venueDto = VenueDto.Create(venue);
        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(venueDto),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
            cancellationToken);

        return TypedResults.Ok(venueDto);
    }
}
