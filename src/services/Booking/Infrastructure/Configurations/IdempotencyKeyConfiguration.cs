using Booking.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booking.Infrastructure.Configurations;

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");

        builder.HasKey(k => k.Key);
        builder.Property(k => k.Key)
            .ValueGeneratedNever()
            .HasMaxLength(200);

        builder.Property(k => k.RequestHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(k => k.ResponseBody)
            .IsRequired();

        builder.HasIndex(k => k.ExpiresAt);
    }
}
