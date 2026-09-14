using Catalog.Features.Events;
using Catalog.Features.Venues;
using Catalog.Infrastructure;
using Contracts.Exceptions;
using Contracts.Middlewares;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails(ProblemDetailsCustomizer.AddExceptionForDevMode);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql
            .EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null));
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = builder.Configuration["catalog:"];
});

// builder.Services.AddSingleton<ElasticsearchClient>(_ =>
// {
//     var settings = new ElasticsearchClientSettings(new Uri(builder.Configuration["Elasticsearch:Url"]!))
//         .DefaultIndex(builder.Configuration["events"]!);
//
//     if (builder.Environment.IsDevelopment())
//     {
//         settings.EnableDebugMode().PrettyJson();
//     }
//
//     return new ElasticsearchClient(settings);
// });

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapEventEndpoints();
app.MapVenueEndpoints();

app.Run();
