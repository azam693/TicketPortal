using Contracts.Commands;
using Contracts.Exceptions;
using Contracts.Middlewares;
using MassTransit;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Order.Features.Orders;
using Order.Infrastructure;
using Order.Infrastructure.Payments;
using Order.Sagas;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails(ProblemDetailsCustomizer.AddExceptionForDevMode);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddDbContext<OrderDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql
            .EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
});

builder.Services.AddScoped<IPaymentGateway, PaymentServiceGateway>();

builder.Services.AddMassTransit(options =>
{
    options.AddSagaStateMachine<OrderStateMachine, Order.Entities.OrderState>()
        .EntityFrameworkRepository(repository =>
        {
            repository.ExistingDbContext<OrderDbContext>();
            repository.UsePostgres();
            repository.ConcurrencyMode = ConcurrencyMode.Optimistic;
        });

    options.AddRequestClient<ProcessPayment>();

    options.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMq"));
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHostedService<OutboxDispatcherService<OrderDbContext>>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapOrderEndpoints();

app.Run();
