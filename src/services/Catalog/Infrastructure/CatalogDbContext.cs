using Catalog.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure;

public class CatalogDbContext : DbContext
{
    public DbSet<Event> Events { get; set; }
    public DbSet<Venue> Venues { get; set; }
}
