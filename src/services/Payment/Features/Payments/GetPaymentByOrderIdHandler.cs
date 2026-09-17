using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Payment.Dtos;
using Payment.Infrastructure;

namespace Payment.Features.Payments;

public static class GetPaymentByOrderIdHandler
{
    public static async Task<Results<Ok<PaymentDto>, ProblemHttpResult>> HandleAsync(
        Guid orderId,
        PaymentDbContext context,
        CancellationToken cancellationToken)
    {
        var payment = await context.Payments
            .FirstOrDefaultAsync(p => p.Id == orderId, cancellationToken);
        if (payment is null)
            return TypedResults.Problem(
                title: "PAYMENT_NOT_FOUND",
                detail: $"Payment for order {orderId} not found.",
                statusCode: StatusCodes.Status404NotFound);

        return TypedResults.Ok(PaymentDto.Create(payment));
    }
}
