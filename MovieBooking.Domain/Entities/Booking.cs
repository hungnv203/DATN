using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class Booking : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public Guid? SeatHoldGroupId { get; set; }
    public string Channel { get; set; } = MovieBooking.Domain.Constants.BookingChannels.CustomerOnline;
    public string Status { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PointDiscountAmount { get; set; }
    public int UsedPoints { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public Payment? Payment { get; set; }
    public ICollection<PaymentOperation> PaymentOperations { get; set; } = new List<PaymentOperation>();
    public ICollection<BookingPromotion> BookingPromotions { get; set; } = new List<BookingPromotion>();
    public ICollection<BookingConcession> BookingConcessions { get; set; } = new List<BookingConcession>();
    public ICollection<MovieReview> MovieReviews { get; set; } = new List<MovieReview>();
    public ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
}
