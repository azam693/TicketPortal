namespace Catalog.Features.Venues;

public static class VenueEndpointExtensions
{
    public static WebApplication MapVenueEndpoints(this WebApplication app)
    {
        var venueEndpoint = app.MapGroup("/api/venues").WithTags("Venues");

        venueEndpoint.MapGet("/{id:guid}", GetVenueByIdHandler.HandleAsync);
        venueEndpoint.MapPost("/", CreateVenueHandler.HandleAsync);

        return app;
    }
}
