using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class Room : BaseEntity
{
    public Guid CinemaId { get; set; }
    public Cinema Cinema { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public string Type { get; set; } = string.Empty;
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
