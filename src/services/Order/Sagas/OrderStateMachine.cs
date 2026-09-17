using Contracts;
using Contracts.Commands;
using Contracts.Events;
using MassTransit;
using Messaging.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Order.Entities;
using Order.Infrastructure;
using Order.Infrastructure.Payments;

namespace Order.Sagas;

/// <summary>
/// Оркестрирует happy path покупки поверх уже существующей Held-брони:
/// Confirm (Booking) -> Payment (мок) -> либо OrderCompleted, либо
/// компенсация Release (Booking) -> OrderFailed. Сам Hold мест в Order не
/// входит — им управляет клиент напрямую через Booking (см.
/// docs/ARCHITECTURE.md, раздел 3.4) — это горячий flash-sale путь, который
/// сага не должна оборачивать лишним хопом через шину.
///
/// Все исходящие команды/события саги идут не напрямую Publish, а через
/// тот же ручной Outbox, что и в Catalog/Booking (OrderDbContext.OutboxMessages
/// + общий OutboxDispatcherService) — иначе переход состояния саги и отправка
/// сообщения не были бы атомарны в одной транзакции.
/// </summary>
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State AwaitingConfirmation { get; private set; } = null!;
    public State AwaitingCompensation { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<SubmitOrder> OrderSubmitted { get; private set; } = null!;
    public Event<BookingConfirmed> ReservationConfirmed { get; private set; } = null!;
    public Event<BookingReleased> ReservationReleased { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(state => state.CurrentState);

        Event(() => OrderSubmitted, x => x.CorrelateById(context => context.Message.ReservationId));
        Event(() => ReservationConfirmed, x => x.CorrelateById(context => context.Message.ReservationId));
        Event(() => ReservationReleased, x => x.CorrelateById(context => context.Message.ReservationId));

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.ReservationId = context.Message.ReservationId;
                    context.Saga.CustomerId = context.Message.CustomerId;
                    context.Saga.Amount = new Money(context.Message.Amount, context.Message.Currency);
                    context.Saga.CreatedAt = DateTimeOffset.UtcNow;

                    Resolve<OrderDbContext>(context).OutboxMessages.Add(
                        new OutboxMessage(new ConfirmReservation(context.Saga.ReservationId)));
                })
                .TransitionTo(AwaitingConfirmation));

        During(AwaitingConfirmation,
            When(ReservationConfirmed)
                .Then(context =>
                {
                    context.Saga.EventId = context.Message.EventId;
                    context.Saga.SeatIds = context.Message.SeatIds.ToList();
                })
                .ThenAsync(async context =>
                {
                    var gateway = Resolve<IPaymentGateway>(context);
                    var result = await gateway.ChargeAsync(
                        context.Saga.CorrelationId, context.Saga.Amount, context.CancellationToken);
                    context.Saga.LastPaymentSucceeded = result == PaymentResult.Succeeded;
                })
                .IfElse(
                    context => context.Saga.LastPaymentSucceeded == true,
                    paid => paid
                        .Then(context =>
                        {
                            context.Saga.CompletedAt = DateTimeOffset.UtcNow;
                            Resolve<OrderDbContext>(context).OutboxMessages.Add(new OutboxMessage(
                                new OrderCompleted(
                                    context.Saga.CorrelationId,
                                    context.Saga.ReservationId,
                                    context.Saga.EventId!.Value,
                                    context.Saga.SeatIds)));
                        })
                        .TransitionTo(Completed),
                    notPaid => notPaid
                        .Then(context => Resolve<OrderDbContext>(context).OutboxMessages.Add(new OutboxMessage(
                            new ReleaseReservation(context.Saga.ReservationId, BookingReleaseReasons.PaymentFailed))))
                        .TransitionTo(AwaitingCompensation)),
            When(ReservationReleased)
                .Then(context => Resolve<OrderDbContext>(context).OutboxMessages.Add(new OutboxMessage(
                    new OrderFailed(context.Saga.CorrelationId, context.Saga.ReservationId, OrderFailureReasons.ReservationExpired))))
                .TransitionTo(Failed),
            Ignore(OrderSubmitted));

        During(AwaitingCompensation,
            When(ReservationReleased)
                .Then(context => Resolve<OrderDbContext>(context).OutboxMessages.Add(new OutboxMessage(
                    new OrderFailed(context.Saga.CorrelationId, context.Saga.ReservationId, OrderFailureReasons.PaymentFailed))))
                .TransitionTo(Failed),
            Ignore(OrderSubmitted));

        // Completed/Failed — обычные терминальные состояния (не встроенный
        // Final): сага не удаляется после завершения, т.к. GET /api/orders/{id}
        // должен продолжать отдавать финальный статус заказа.
        During(Completed,
            Ignore(OrderSubmitted),
            Ignore(ReservationConfirmed),
            Ignore(ReservationReleased));

        During(Failed,
            Ignore(OrderSubmitted),
            Ignore(ReservationConfirmed),
            Ignore(ReservationReleased));
    }

    /// <summary>
    /// Сам стейт-машин — синглтон, поэтому scoped-сервисы (DbContext,
    /// платёжный шлюз) резолвятся не через конструктор, а через DI-контейнер,
    /// который MassTransit кладёт в payload контекста на каждое сообщение.
    /// OrderDbContext резолвится тем же способом, что и в
    /// EntityFrameworkRepository (ExistingDbContext) — поэтому OutboxMessages,
    /// добавленные здесь, попадают в тот же SaveChanges, что сохраняет
    /// состояние саги.
    /// </summary>
    private static TService Resolve<TService>(PipeContext context) where TService : notnull =>
        context.GetPayload<IServiceProvider>().GetRequiredService<TService>();
}
