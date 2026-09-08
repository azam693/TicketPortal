using Catalog.Dtos;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Features.Events;

public static class ListEventsHandler
{
    private const int MaxPageSize = 100;

    public static async Task<Ok<PagedResult<EventDto>>> HandleAsync(
        CatalogDbContext context,
        CancellationToken cancellationToken,
        string? search = null,
        string? city = null,
        int page = 1,
        int pageSize = 20)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? 20 : pageSize;

        var query = context.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(@event =>
                EF.Functions.ILike(@event.Title, $"%{term}%") ||
                EF.Functions.ILike(@event.Description, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var term = city.Trim();
            query = query.Where(@event => context.Venues
                .Any(venue => venue.Id == @event.VenueId && EF.Functions.ILike(venue.City, term)));
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var events = await query
            .OrderBy(@event => @event.StartsAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = events.Select(EventDto.Create).ToList();

        return TypedResults.Ok(new PagedResult<EventDto>(items, page, pageSize, totalCount));
    }
}
