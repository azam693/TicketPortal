using CommunityToolkit.Diagnostics;
using Contracts.Exceptions;

namespace Catalog.Entities;

public class Event   
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public Guid VenueId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public EventStatuses Status { get; private set; }
    public DateTimeOffset SalesStartAt { get; private set; }

    public Event(
        string title,
        string description,
        Guid venueId,
        DateTimeOffset startsAt,
        DateTimeOffset salesStartAt)
    {
        Guard.IsNotNullOrWhiteSpace(title);
        Guard.IsNotNullOrWhiteSpace(description);
        
        if (salesStartAt >= startsAt)
            throw new DomainException("Sales must start before the event");
        
        Title = title.Trim();
        Description = description.Trim();
        VenueId = venueId;
        StartsAt = startsAt;
        SalesStartAt = salesStartAt;
        Status = EventStatuses.Draft;
    }

    public void Update(
        string title,
        string description,
        DateTimeOffset startsAt,
        DateTimeOffset salesStartAt)
    {
        Guard.IsNotNullOrWhiteSpace(title);
        Guard.IsNotNullOrWhiteSpace(description);

        if (salesStartAt >= startsAt)
            throw new DomainException("Sales must start before the event");

        Title = title.Trim();
        Description = description.Trim();
        StartsAt = startsAt;
        SalesStartAt = salesStartAt;
    }

    public void Publish()
    {
        if (Status == EventStatuses.Cancelled)
            throw new DomainException("Cancelled event cannot be published");

        Status = EventStatuses.Published;
    }

    public void Cancel()
    {
        if (Status == EventStatuses.Cancelled)
            throw new DomainException("Event is already cancelled");

        Status = EventStatuses.Cancelled;
    }
}
