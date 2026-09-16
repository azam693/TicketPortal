using Booking.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Booking.Infrastructure.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        // Xmin как токен optimistic concurrency: защищает от гонки между
        // ReservationExpirationSweeper и Confirm/Release-эндпоинтами,
        // которые могут одновременно трогать одну и ту же бронь.
        builder.Property(r => r.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.OwnsMany(r => r.Seats, seat =>
        {
            seat.ToTable("reservation_seats");
            seat.WithOwner().HasForeignKey(s => s.ReservationId);
            seat.HasKey(s => s.Id);
            seat.Property(s => s.Id).ValueGeneratedNever();

            // Не уникальный: одно и то же место проходит через несколько
            // броней за свою жизнь (Released/Expired -> снова Available -> Held).
            seat.HasIndex(s => s.SeatId);
        });

        builder.Navigation(r => r.Seats)
            .HasField("_seats")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(r => new { r.Status, r.ExpiresAt });
    }
}
