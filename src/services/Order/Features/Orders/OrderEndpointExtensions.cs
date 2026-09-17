namespace Order.Features.Orders;

public static class OrderEndpointExtensions
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        var orderEndpoint = app.MapGroup("/api/orders").WithTags("Orders");

        orderEndpoint.MapPost("/", CreateOrderHandler.HandleAsync);
        orderEndpoint.MapGet("/{id:guid}", GetOrderByIdHandler.HandleAsync);

        return app;
    }
}
