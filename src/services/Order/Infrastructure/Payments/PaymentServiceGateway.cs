using Contracts;
using Contracts.Commands;
using Contracts.Events;
using MassTransit;

namespace Order.Infrastructure.Payments;

/// <summary>
/// Реализация IPaymentGateway поверх отдельного сервиса Payment (см.
/// docs/ARCHITECTURE.md, раздел 3.5) — request/response через шину
/// (MassTransit IRequestClient), а не Publish/Consume интеграционного
/// события: сага должна получить результат оплаты синхронно в рамках
/// текущей обработки BookingConfirmed, а не продолжать работу по отдельному
/// входящему сообщению. Замена прежней FakePaymentGateway не потребовала
/// изменений в OrderStateMachine — она видит только интерфейс
/// IPaymentGateway.
/// </summary>
public class PaymentServiceGateway(IRequestClient<ProcessPayment> client) : IPaymentGateway
{
    public async Task<PaymentResult> ChargeAsync(Guid orderId, Money amount, CancellationToken cancellationToken)
    {
        var response = await client.GetResponse<PaymentProcessed>(
            new ProcessPayment(orderId, amount), cancellationToken);

        return response.Message.Outcome == PaymentOutcomes.Succeeded
            ? PaymentResult.Succeeded
            : PaymentResult.Failed;
    }
}
