using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Booking.Dtos;
using Booking.Entities;
using Booking.Infrastructure;
using Booking.Infrastructure.Locking;
using Contracts.Events;
using Messaging.Idempotency;
using Messaging.Outbox;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Bookings;

public static class CreateReservationHandler
{
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IdempotencyKeyTtl = TimeSpan.FromHours(24);

    public static async Task<Results<Created<ReservationDto>, ProblemHttpResult>> HandleAsync(
        CreateReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        BookingDbContext context,
        IDistributedLockService lockService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return TypedResults.Problem(
                title: "IDEMPOTENCY_KEY_REQUIRED",
                detail: "Idempotency-Key header is required.",
                statusCode: StatusCodes.Status400BadRequest);

        var seatIds = request.SeatIds.Distinct().OrderBy(id => id).ToList();
        var requestHash = ComputeHash($"{request.EventId}:{string.Join(",", seatIds)}");

        var existingKey = await context.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key == idempotencyKey, cancellationToken);
        if (existingKey is not null)
        {
            if (existingKey.RequestHash != requestHash)
                return TypedResults.Problem(
                    title: "IDEMPOTENCY_KEY_REUSED",
                    detail: "Idempotency-Key was already used with a different request body.",
                    statusCode: StatusCodes.Status409Conflict);

            var cachedDto = JsonSerializer.Deserialize<ReservationDto>(existingKey.ResponseBody)!;
            return TypedResults.Created($"/api/bookings/{cachedDto.Id}", cachedDto);
        }

        await using var @lock = await lockService.TryAcquireAsync(
            $"lock:event:{request.EventId}:seats", LockTimeout, cancellationToken);
        if (@lock is null)
            return TypedResults.Problem(
                title: "SEATS_LOCK_BUSY",
                detail: "Too many concurrent requests for this event's seats, retry.",
                statusCode: StatusCodes.Status409Conflict);

        var seats = await context.Seats
            .Where(seat => seat.EventId == request.EventId && seatIds.Contains(seat.Id))
            .ToListAsync(cancellationToken);

        if (seats.Count != seatIds.Count)
            return TypedResults.Problem(
                title: "SEAT_NOT_FOUND",
                detail: "One or more requested seats do not exist for this event.",
                statusCode: StatusCodes.Status404NotFound);

        var unavailable = seats.Where(seat => seat.Status != SeatStatuses.Available).ToList();
        if (unavailable.Count > 0)
            return TypedResults.Problem(
                title: "SEAT_NOT_AVAILABLE",
                detail: $"Seats already taken: {string.Join(", ", unavailable.Select(seat => seat.Id))}.",
                statusCode: StatusCodes.Status409Conflict);

        foreach (var seat in seats)
        {
            seat.Hold();
        }

        var reservation = new Reservation(request.EventId, null, seatIds, HoldDuration);
        await context.Reservations.AddAsync(reservation, cancellationToken);

        var integrationEvent = new SeatsHeld(reservation.Id, reservation.EventId, seatIds, reservation.ExpiresAt);
        context.OutboxMessages.Add(new OutboxMessage(integrationEvent));

        var dto = ReservationDto.Create(reservation);
        context.IdempotencyKeys.Add(new IdempotencyKey(
            idempotencyKey,
            requestHash,
            StatusCodes.Status201Created,
            JsonSerializer.Serialize(dto),
            IdempotencyKeyTtl));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                title: "SEAT_NOT_AVAILABLE",
                detail: "One of the requested seats was taken concurrently, retry.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.Created($"/api/bookings/{reservation.Id}", dto);
    }

    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public record CreateReservationRequest(
    [Required] Guid EventId,
    [Required, MinLength(1)] IReadOnlyList<Guid> SeatIds);
