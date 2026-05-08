using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class Seat : BaseEntity
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<SeatHold> SeatHolds { get; set; } = new List<SeatHold>();
}
