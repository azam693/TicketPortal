namespace Catalog.Features.Events;

public static class EventEndpointExtensions
{
    public static WebApplication MapEventEndpoints(this WebApplication app)
    {
        var eventEndpoint = app.MapGroup("/api/events").WithTags("Events");

        eventEndpoint.Map("/{id:guid}", GetEventByIdHandler.HandleAsync);
        eventEndpoint.MapPost("/", CreateEventHandler.HandleAsync);
        
        return app;
    }
}
