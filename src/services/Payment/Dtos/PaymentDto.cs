using Contracts;
using Payment.Entities;

namespace Payment.Dtos;

public record PaymentDto(
    Guid OrderId,
    Money Amount,
    string Status,
    DateTimeOffset ProcessedAt)
{
    public static PaymentDto Create(PaymentTransaction payment) => new(
        payment.Id,
        payment.Amount,
        payment.Status.ToString(),
        payment.ProcessedAt);
}
