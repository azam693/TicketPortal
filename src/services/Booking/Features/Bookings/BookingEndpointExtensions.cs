namespace Booking.Features.Bookings;

public static class BookingEndpointExtensions
{
    public static WebApplication MapBookingEndpoints(this WebApplication app)
    {
        var bookingEndpoint = app.MapGroup("/api/bookings").WithTags("Bookings");

        bookingEndpoint.MapPost("/", CreateReservationHandler.HandleAsync);
        bookingEndpoint.MapGet("/{id:guid}", GetReservationByIdHandler.HandleAsync);
        bookingEndpoint.MapPost("/{id:guid}/confirm", ConfirmReservationHandler.HandleAsync);
        bookingEndpoint.MapPost("/{id:guid}/release", ReleaseReservationHandler.HandleAsync);

        return app;
    }
}
