using Contracts;
using Contracts.Exceptions;

namespace Payment.Entities;

public enum PaymentStatuses
{
    Succeeded,
    Failed
}

/// <summary>
/// Класс намеренно не называется Payment — совпадение с корневым
/// неймспейсом сервиса (Payment) ловится компилятором как коллизия
/// (CS0118), см. аналогичное решение для Reservation/Booking в
/// docs/ARCHITECTURE.md, раздел 3.2.
///
/// Id = OrderId (он же ReservationId саги Order) — бизнес-ключ
/// идемпотентности платежа, а не сгенерированный локально Id (см.
/// docs/ARCHITECTURE.md, раздел 3.5): повторный ProcessPayment с тем же
/// OrderId (redelivery запроса при at-least-once доставке через шину)
/// находит уже сохранённую запись и возвращает её результат вместо
/// повторного списания у провайдера.
/// </summary>
public class PaymentTransaction
{
    public Guid Id { get; private set; }
    public Money Amount { get; private set; }
    public PaymentStatuses Status { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    private PaymentTransaction()
    {
    }

    public PaymentTransaction(Guid orderId, Money amount, PaymentStatuses status)
    {
        if (amount.Amount <= 0)
            throw new DomainException("Payment amount must be positive.");

        Id = orderId;
        Amount = amount;
        Status = status;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
