using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapScalarApiReference(options =>
{
    options
        .AddDocument("catalog", "Catalog", "/openapi/catalog.json")
        .AddDocument("booking", "Booking", "/openapi/booking.json")
        .AddDocument("order", "Order", "/openapi/order.json")
        .AddDocument("payment", "Payment", "/openapi/payment.json");
});

app.MapReverseProxy();

app.Run();
