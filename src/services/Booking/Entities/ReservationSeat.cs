namespace Booking.Entities;

public class ReservationSeat
{
    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid SeatId { get; private set; }

    private ReservationSeat()
    {
    }

    public ReservationSeat(Guid reservationId, Guid seatId)
    {
        Id = Guid.NewGuid();
        ReservationId = reservationId;
        SeatId = seatId;
    }
}
