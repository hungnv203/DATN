using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class SeatHold : BaseEntity
{
    public Guid SessionId { get; set; }
    public Guid ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public Guid SeatId { get; set; }
    public Seat Seat { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Status { get; set; } = SeatHoldStatuses.Active;
    public DateTime ExpiredAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
}

public static class SeatHoldStatuses
{
    public const string Active = "Active";
    public const string Released = "Released";
    public const string Expired = "Expired";
    public const string Completed = "Completed";
}
