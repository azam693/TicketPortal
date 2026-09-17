using Contracts.Commands;
using Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Payment.Entities;
using Payment.Infrastructure;

namespace Payment.Features.Payments;

/// <summary>
/// Request/response, а не Publish/Consume интеграционного события: сага
/// Order должна получить результат оплаты синхронно в рамках текущей
/// обработки BookingConfirmed (см. docs/ARCHITECTURE.md, раздел 3.4/3.5), а
/// не продолжать работу по отдельному входящему сообщению.
/// </summary>
public class ProcessPaymentConsumer(
    PaymentDbContext dbContext,
    IOptions<PaymentGatewayOptions> options) : IConsumer<ProcessPayment>
{
    public async Task Consume(ConsumeContext<ProcessPayment> context)
    {
        var orderId = context.Message.OrderId;

        var existing = await dbContext.Payments
            .FirstOrDefaultAsync(payment => payment.Id == orderId, context.CancellationToken);
        if (existing is not null)
        {
            await context.RespondAsync(new PaymentProcessed(orderId, ToOutcome(existing.Status)));
            return;
        }

        // Мок реального провайдера (Stripe и т.п., пока не подключён) со
        // намеренной нестабильностью — для отработки retry/идемпотентности.
        var failed = Random.Shared.NextDouble() < options.Value.FailureRate;
        var status = failed ? PaymentStatuses.Failed : PaymentStatuses.Succeeded;

        dbContext.Payments.Add(new PaymentTransaction(orderId, context.Message.Amount, status));
        await dbContext.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new PaymentProcessed(orderId, ToOutcome(status)));
    }

    private static PaymentOutcomes ToOutcome(PaymentStatuses status) =>
        status == PaymentStatuses.Succeeded ? PaymentOutcomes.Succeeded : PaymentOutcomes.Failed;
}
