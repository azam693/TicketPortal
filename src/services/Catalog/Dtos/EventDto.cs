using Catalog.Entities;

namespace Catalog.Dtos;

public record EventDto(
    Guid Id,
    string Title,
    string Description,
    Guid VenueId,
    DateTimeOffset StartsAt,
    string Status,
    DateTimeOffset SalesStartAt)
{
    public static EventDto Create(Event @event) =>
        new(
            @event.Id,
            @event.Title,
            @event.Description,
            @event.VenueId,
            @event.StartsAt,
            @event.Status.ToString(),
            @event.SalesStartAt);
}
