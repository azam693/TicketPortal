namespace Booking.Features.EventSeats;

public static class EventSeatsEndpointExtensions
{
    public static WebApplication MapEventSeatsEndpoints(this WebApplication app)
    {
        app.MapGroup("/api/events").WithTags("EventSeats")
            .MapGet("/{eventId:guid}/seats", GetEventSeatsHandler.HandleAsync);

        return app;
    }
}
