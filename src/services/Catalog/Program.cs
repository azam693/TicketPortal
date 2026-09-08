using Catalog.Features.Events;
using Catalog.Infrastructure;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.MapEventEndpoints();

app.Run();
