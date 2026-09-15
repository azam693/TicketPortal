using System.ComponentModel.DataAnnotations;
using Catalog.Entities;
using Catalog.Infrastructure;
using Contracts;
using Contracts.Events;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Catalog.Features.Events;

public static class PublishEventHandler
{
    public static async Task<Results<Ok, ProblemHttpResult>> HandleAsync(
        Guid id,
        PublishEventRequest request,
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

        if (@event.Status == EventStatuses.Published)
            return TypedResults.Problem(
                title: "EVENT_ALREADY_PUBLISHED",
                detail: $"Event with id {id} is already published.",
                statusCode: StatusCodes.Status409Conflict);

        var venue = await context.Venues
            .FirstOrDefaultAsync(venue => venue.Id == @event.VenueId, cancellationToken);
        if (venue is null)
            return TypedResults.Problem(
                title: "VENUE_NOT_FOUND",
                detail: $"Venue with id {@event.VenueId} not found.",
                statusCode: StatusCodes.Status404NotFound);

        var prices = request.Prices.ToDictionary(p => p.Category, p => new Money(p.Amount, p.Currency));
        var missingCategories = venue.Sections
            .Select(section => section.Category)
            .Distinct()
            .Where(category => !prices.ContainsKey(category))
            .ToList();
        if (missingCategories.Count > 0)
            return TypedResults.Problem(
                title: "MISSING_SEAT_PRICES",
                detail: $"No price provided for categories: {string.Join(", ", missingCategories)}.",
                statusCode: StatusCodes.Status400BadRequest);

        var seats = venue.Sections
            .SelectMany(section =>
                Enumerable.Range(1, section.RowsCount).SelectMany(row =>
                    Enumerable.Range(1, section.SeatsPerRow).Select(number =>
                        new Seat(@event.Id, section.Id, row, number, prices[section.Category]))))
            .ToList();

        await context.Seats.AddRangeAsync(seats, cancellationToken);

        @event.Publish();
        
        var sectionCategories = venue.Sections
            .ToDictionary(section => section.Id, section => section.Category.ToString());
        var integrationEvent = new EventPublished(
            @event.Id,
            venue.Id,
            seats
                .Select(s => new SeatSnapshot(
                    s.Id, s.SectionId, s.Row, s.Number, s.Price.Amount,
                    s.Price.Currency, sectionCategories[s.SectionId]))
                .ToList());
        context.OutboxMessages.Add(new OutboxMessage(integrationEvent));

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync($"event:{id}", cancellationToken);

        return TypedResults.Ok();
    }
}

public record PublishEventRequest([Required] IReadOnlyList<SeatCategoryPriceRequest> Prices);

public record SeatCategoryPriceRequest(
    SeatCategories Category,
    [property: Range(0.01, double.MaxValue)] decimal Amount,
    [property: Required] string Currency);
