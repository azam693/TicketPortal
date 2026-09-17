using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Entities;

namespace Order.Infrastructure.Configurations;

public class OrderStateConfiguration : IEntityTypeConfiguration<OrderState>
{
    public void Configure(EntityTypeBuilder<OrderState> builder)
    {
        builder.ToTable("order_states");

        builder.HasKey(state => state.CorrelationId);
        builder.Property(state => state.CorrelationId).ValueGeneratedNever();

        builder.Property(state => state.CurrentState).HasMaxLength(64);

        builder.ComplexProperty(
            state => state.Amount,
            amount => amount.Property(p => p.Currency).HasMaxLength(3));

        // Xmin как токен optimistic concurrency для EF saga-репозитория
        // MassTransit — тот же приём, что и в Booking (Seat/Reservation).
        builder.Property(state => state.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(state => state.ReservationId).IsUnique();
    }
}
