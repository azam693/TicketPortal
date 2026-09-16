using Contracts.Exceptions;

namespace Booking.Entities;

public class Reservation
{
    private readonly List<ReservationSeat> _seats = [];

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public ReservationStatuses Status { get; private set; }
    public IReadOnlyList<ReservationSeat> Seats => _seats;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }

    private Reservation()
    {
    }

    public Reservation(
        Guid eventId,
        Guid? customerId,
        IReadOnlyCollection<Guid> seatIds,
        TimeSpan holdDuration)
    {
        if (seatIds.Count == 0)
            throw new DomainException("Reservation must include at least one seat");

        Id = Guid.NewGuid();
        EventId = eventId;
        CustomerId = customerId;
        Status = ReservationStatuses.Held;
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = CreatedAt.Add(holdDuration);

        _seats.AddRange(seatIds.Select(seatId => new ReservationSeat(Id, seatId)));
    }

    public void Confirm()
    {
        if (Status != ReservationStatuses.Held)
            throw new DomainException("Only a held reservation can be confirmed");

        Status = ReservationStatuses.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;
    }

    public void Release()
    {
        if (Status != ReservationStatuses.Held)
            throw new DomainException("Only a held reservation can be released");

        Status = ReservationStatuses.Released;
    }

    public void Expire()
    {
        if (Status != ReservationStatuses.Held)
            throw new DomainException("Only a held reservation can expire");

        Status = ReservationStatuses.Expired;
    }
}
