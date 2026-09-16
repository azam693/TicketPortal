using Booking.Entities;
using Booking.Infrastructure;
using Contracts.Events;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Booking.BackgroundServices;

public class ReservationExpirationSweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationExpirationSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepExpiredReservationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Reservation expiration sweep iteration failed");
            }
        }
    }

    private async Task SweepExpiredReservationsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var now = DateTimeOffset.UtcNow;
        var reservations = await context.Reservations
            .Where(reservation => reservation.Status == ReservationStatuses.Held && reservation.ExpiresAt < now)
            .OrderBy(reservation => reservation.ExpiresAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (reservations.Count == 0)
            return;

        foreach (var reservation in reservations)
        {
            try
            {
                await ExpireReservationAsync(context, reservation, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Confirm/Release успели изменить бронь раньше sweeper'а — состояние
                // уже корректно разрулено тем запросом, откатываем и идём дальше.
                context.ChangeTracker.Clear();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to expire reservation {ReservationId}", reservation.Id);
                context.ChangeTracker.Clear();
            }
        }
    }

    private static async Task ExpireReservationAsync(
        BookingDbContext context,
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        reservation.Expire();

        var seatIds = reservation.Seats.Select(seat => seat.SeatId).ToList();
        var seats = await context.Seats
            .Where(seat => seatIds.Contains(seat.Id))
            .ToListAsync(cancellationToken);

        foreach (var seat in seats)
        {
            seat.Release();
        }

        context.OutboxMessages.Add(new OutboxMessage(
            new BookingReleased(reservation.Id, reservation.EventId, seatIds, BookingReleaseReasons.Expired)));

        await context.SaveChangesAsync(cancellationToken);
    }
}
