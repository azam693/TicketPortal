using System.Net;
using System.Net.Http.Json;

namespace Catalog.Tests.Integration.Events;

public class EventsEndpointsTests : IClassFixture<CatalogWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EventsEndpointsTests(CatalogWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateEvent_WhenVenueDoesNotExist_ReturnsBadRequest()
    {
        var request = new
        {
            Title = "Concert",
            Description = "Description",
            VenueId = Guid.NewGuid(),
            StartsAt = DateTime.UtcNow.AddDays(10),
            SalesStartAt = DateTime.UtcNow.AddDays(1)
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    
    [Fact]
    public async Task GetEventById_WhenEventDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/events/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
