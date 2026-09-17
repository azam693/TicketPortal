using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Order.Dtos;
using Order.Infrastructure;

namespace Order.Features.Orders;

public static class GetOrderByIdHandler
{
    public static async Task<Results<Ok<OrderDto>, ProblemHttpResult>> HandleAsync(
        Guid id,
        OrderDbContext context,
        CancellationToken cancellationToken)
    {
        var state = await context.OrderStates
            .AsNoTracking()
            .FirstOrDefaultAsync(state => state.CorrelationId == id, cancellationToken);
        if (state is null)
            return TypedResults.Problem(
                title: "ORDER_NOT_FOUND",
                detail: $"Order with id {id} not found.",
                statusCode: StatusCodes.Status404NotFound);

        return TypedResults.Ok(OrderDto.Create(state));
    }
}
