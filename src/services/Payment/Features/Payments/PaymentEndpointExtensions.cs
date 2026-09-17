namespace Payment.Features.Payments;

public static class PaymentEndpointExtensions
{
    public static WebApplication MapPaymentEndpoints(this WebApplication app)
    {
        var paymentEndpoint = app.MapGroup("/api/payments").WithTags("Payments");

        paymentEndpoint.MapGet("/{orderId:guid}", GetPaymentByOrderIdHandler.HandleAsync);

        return app;
    }
}
