using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class Showtime : BaseEntity
{
    public Guid MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal BasePrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<SeatHold> SeatHolds { get; set; } = new List<SeatHold>();
    public ShowtimeSeatVersion? SeatVersion { get; set; }
}
