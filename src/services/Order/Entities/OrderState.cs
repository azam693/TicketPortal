using Contracts;
using MassTransit;

namespace Order.Entities;

/// <summary>
/// Состояние саги Order. В отличие от остальных агрегатов проекта (приватные
/// сеттеры, инварианты в методах) — это анемичный instance-класс с публичными
/// сеттерами: так того требует MassTransitStateMachine, вся бизнес-логика
/// переходов живёт в OrderStateMachine, а не здесь.
///
/// CorrelationId саги намеренно равен ReservationId, а не отдельно
/// сгенерированному Id: одна Held-бронь конвертируется ровно в один заказ,
/// и это делает корреляцию входящих событий Booking (BookingConfirmed/
/// BookingReleased, которые несут ReservationId, а не OrderId) тривиальной —
/// без отдельной таблицы соответствий.
/// </summary>
public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = default!;

    public Guid ReservationId { get; set; }
    public Guid? EventId { get; set; }
    public Guid? CustomerId { get; set; }
    public List<Guid> SeatIds { get; set; } = [];
    public Money Amount { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Скретч-поле только для ветвления внутри одной обработки
    /// BookingConfirmed (результат мок-оплаты), частью публичного API заказа
    /// не является.
    /// </summary>
    public bool? LastPaymentSucceeded { get; set; }

    public uint RowVersion { get; set; }
}
