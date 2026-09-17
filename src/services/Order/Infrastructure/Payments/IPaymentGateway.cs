using Contracts;

namespace Order.Infrastructure.Payments;

public enum PaymentResult
{
    Succeeded,
    Failed
}

/// <summary>
/// Абстракция шлюза оплаты. Сейчас единственная реализация — FakePaymentGateway
/// (мок со случайным отказом), сага дёргает её синхронно внутри своей
/// транзакции. Когда появится отдельный сервис Payment (Step 4/5), эта
/// реализация заменяется на request/response через шину — сама сага
/// (OrderStateMachine) не должна при этом измениться, т.к. видит только
/// интерфейс.
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(Guid orderId, Money amount, CancellationToken cancellationToken);
}
