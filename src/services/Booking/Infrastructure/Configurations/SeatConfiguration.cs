using Booking.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booking.Infrastructure.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.ToTable("seats");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Category)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        builder.ComplexProperty(
            s => s.Price,
            price => price.Property(p => p.Currency).HasMaxLength(3));

        // Xmin как токен optimistic concurrency: защищает переход
        // Available -> Held от гонки параллельных запросов на одно место.
        builder.Property(s => s.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(s => new { s.EventId, s.Status });
        builder.HasIndex(s => new { s.EventId, s.SectionId, s.Row, s.Number }).IsUnique();
    }
}
