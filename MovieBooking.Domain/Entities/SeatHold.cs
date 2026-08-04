using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class SeatHold : BaseEntity
{
    public Guid HoldGroupId { get; set; }
    public Guid ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public Guid SeatId { get; set; }
    public Seat Seat { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public string Status { get; set; } = MovieBooking.Domain.Constants.SeatHoldStatuses.Active;
    public DateTime ExpiredAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
