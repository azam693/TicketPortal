using Booking.Features.Bookings;
using Booking.Features.EventSeats;
using Booking.Features.Integration;
using Booking.Infrastructure;
using Booking.Infrastructure.Locking;
using Contracts.Exceptions;
using Contracts.Middlewares;
using MassTransit;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails(ProblemDetailsCustomizer.AddExceptionForDevMode);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddDbContext<BookingDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql
            .EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
});

builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddSingleton<IDistributedLockService, RedisDistributedLockService>();

builder.Services.AddMassTransit(options =>
{
    options.AddConsumer<EventPublishedConsumer>();

    options.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMq"));
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHostedService<OutboxDispatcherService<BookingDbContext>>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapBookingEndpoints();
app.MapEventSeatsEndpoints();

app.Run();
