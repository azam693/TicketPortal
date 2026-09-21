using Catalog.Entities;
using Catalog.Features.Events;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Tests.Unit.Features.Events;

public class CreateEventHandlerTests
{
    private static CatalogDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task HandleAsync_WhenVenueDoesNotExist_ReturnsProblem()
    {
        await using var context = CreateDbContext();
        var request = new CreateEventRequest(
            "Concert", "Description", Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(1));
        var result = await CreateEventHandler.HandleAsync(request, context, CancellationToken.None);

        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal("VENUE_NOT_FOUND", problem.ProblemDetails.Title);
    }
    
    [Fact]
    public async Task HandleAsync_WhenVenueExists_CreatesEventAndReturnsOk()
    {
        await using var context = CreateDbContext();
        var venue = new Venue("Arena", "Astana", "Main St 1",
            [new SeatMapSection("A", 10, 100, SeatCategories.Standard)]);
        await context.Venues.AddAsync(venue);
        await context.SaveChangesAsync();

        var request = new CreateEventRequest(
            "Concert", "Description", venue.Id,
            DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(1));

        var result = await CreateEventHandler.HandleAsync(request, context, CancellationToken.None);

        var ok = Assert.IsType<Ok<Guid>>(result.Result);
        Assert.NotEqual(Guid.Empty, ok.Value);
        Assert.Equal(1, await context.Events.CountAsync());
    }
}
