namespace Payment.Infrastructure;

/// <summary>Настройки мока реального провайдера (Stripe и т.п.), пока не подключённого.</summary>
public class PaymentGatewayOptions
{
    /// <summary>Доля запросов, которые мок намеренно проваливает (0.0-1.0).</summary>
    public double FailureRate { get; set; } = 0.2;
}
