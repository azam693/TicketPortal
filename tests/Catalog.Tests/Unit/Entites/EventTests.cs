using Catalog.Entities;
using Contracts.Exceptions;

namespace Catalog.Tests.Unit.Entites;

public class EventTests
{
    [Fact]
    public void Constructor_WhenSalesStartAfterEventStart_ThrowsDomainException()
    {
        var startsAt =  DateTimeOffset.UtcNow.AddDays(10);
        var salesStartAt = startsAt.AddDays(1);

        var act = () => new Event("Concert", "Description", Guid.NewGuid(), startsAt, salesStartAt);

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Publish_WhenEventIsCancelled_ThrowsDomainException()
    {
        var @event = new Event(
            "Concert",
            "Description",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(10),
            DateTimeOffset.UtcNow.AddDays(1));
        @event.Cancel();

        var act = @event.Publish;

        Assert.Throws<DomainException>(act);
    }
}
