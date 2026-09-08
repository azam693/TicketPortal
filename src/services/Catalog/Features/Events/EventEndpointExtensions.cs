namespace Catalog.Features.Events;

public static class EventEndpointExtensions
{
    public static WebApplication MapEventEndpoints(this WebApplication app)
    {
        var eventEndpoint = app.MapGroup("/api/events").WithTags("Events");

        eventEndpoint.MapGet("/", ListEventsHandler.HandleAsync);
        eventEndpoint.MapGet("/{id:guid}", GetEventByIdHandler.HandleAsync);
        eventEndpoint.MapPost("/", CreateEventHandler.HandleAsync);
        eventEndpoint.MapPut("/{id:guid}", UpdateEventHandler.HandleAsync);
        eventEndpoint.MapPost("/{id:guid}/publish", PublishEventHandler.HandleAsync);
        eventEndpoint.MapPost("/{id:guid}/cancel", CancelEventHandler.HandleAsync);

        return app;
    }
}
