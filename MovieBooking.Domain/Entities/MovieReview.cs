using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class MovieReview : BaseEntity
{
    public Guid MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string Status { get; set; } = "Visible";
}
