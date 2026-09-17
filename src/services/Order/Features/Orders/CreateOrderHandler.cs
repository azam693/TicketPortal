using System.ComponentModel.DataAnnotations;
using Contracts.Commands;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Order.Features.Orders;

public static class CreateOrderHandler
{
    public static async Task<Accepted<CreateOrderResponse>> HandleAsync(
        CreateOrderRequest request,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        await publishEndpoint.Publish(
            new SubmitOrder(request.ReservationId, request.CustomerId, request.Amount, request.Currency),
            cancellationToken);

        var response = new CreateOrderResponse(request.ReservationId);
        return TypedResults.Accepted($"/api/orders/{request.ReservationId}", response);
    }
}

public record CreateOrderRequest(
    [Required] Guid ReservationId,
    Guid? CustomerId,
    [Range(0.01, double.MaxValue)] decimal Amount,
    [Required, MinLength(3), MaxLength(3)] string Currency);

/// <summary>
/// Id заказа = Id уже существующей Held-брони (см. OrderState). Сага
/// обрабатывает SubmitOrder асинхронно, поэтому 202 Accepted (а не 201
/// Created, как у синхронного Booking) — на момент ответа заказа ещё не
/// существует, клиент узнаёт результат через GET /api/orders/{id}.
/// </summary>
public record CreateOrderResponse(Guid OrderId);
