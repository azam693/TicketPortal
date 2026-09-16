using Booking.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booking.Infrastructure.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(outbox => outbox.Id);
        builder.Property(outbox => outbox.Id).ValueGeneratedNever();

        builder.Property(outbox => outbox.Type)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(outbox => outbox.Content)
            .IsRequired();

        builder.HasIndex(outbox => outbox.ProcessedAt);
    }
}
