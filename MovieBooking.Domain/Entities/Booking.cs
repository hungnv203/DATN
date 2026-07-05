using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class Booking : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public string Status { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public Payment? Payment { get; set; }
    public ICollection<BookingPromotion> BookingPromotions { get; set; } = new List<BookingPromotion>();
    public ICollection<BookingConcession> BookingConcessions { get; set; } = new List<BookingConcession>();
}
