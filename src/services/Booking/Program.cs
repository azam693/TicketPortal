using Booking.Infrastructure;
using Contracts.Exceptions;
using Contracts.Middlewares;
using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
