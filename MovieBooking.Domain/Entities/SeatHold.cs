using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class SeatHold : BaseEntity
{
    public Guid ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public Guid SeatId { get; set; }
    public Seat Seat { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime ExpiredAt { get; set; }
}
