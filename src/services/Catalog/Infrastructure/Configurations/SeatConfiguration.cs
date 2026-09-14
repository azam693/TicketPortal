using Catalog.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.ToTable("seats");
        
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();
        
        builder.ComplexProperty(
            s => s.Price,
            price => price.Property(p => p.Currency).HasMaxLength(3));

        builder.HasIndex(s => new { s.EventId, s.SectionId, s.Row, s.Number }).IsUnique();
    }
}
