using Contracts.Exceptions;
using Contracts.Middlewares;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.Features.Payments;
using Payment.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(ProblemDetailsCustomizer.AddExceptionForDevMode);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddDbContext<PaymentDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql
            .EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
});

builder.Services.Configure<PaymentGatewayOptions>(builder.Configuration.GetSection("PaymentGateway"));

builder.Services.AddMassTransit(options =>
{
    options.AddConsumer<ProcessPaymentConsumer>();

    options.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMq"));
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPaymentEndpoints();

app.Run();
