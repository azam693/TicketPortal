using System.ComponentModel.DataAnnotations;
using Catalog.Entities;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Catalog.Features.Venues;

public static class CreateVenueHandler
{
    public static async Task<Ok<Guid>> HandleAsync(
        CreateVenueRequest request,
        CatalogDbContext context,
        CancellationToken cancellationToken)
    {
        var sections = (request.Sections ?? [])
            .Select(section => new SeatMapSection(
                section.Name,
                section.RowsCount,
                section.SeatsPerRow,
                section.Category));

        var venue = new Venue(request.Name, request.City, request.Address, sections);

        await context.Venues.AddAsync(venue, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(venue.Id);
    }
}

public record CreateVenueRequest(
    [Required] string Name,
    [Required] string City,
    [Required] string Address,
    [Required] IReadOnlyList<CreateSeatMapSectionRequest> Sections);

public record CreateSeatMapSectionRequest(
    [Required] string Name,
    int RowsCount,
    int SeatsPerRow,
    SeatCategories Category);
