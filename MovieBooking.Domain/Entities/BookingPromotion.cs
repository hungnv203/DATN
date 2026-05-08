using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class BookingPromotion : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public Guid PromotionId { get; set; }
    public Promotion Promotion { get; set; } = null!;
    public decimal DiscountAmount { get; set; }
}
