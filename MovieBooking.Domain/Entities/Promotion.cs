using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class Promotion : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal MinOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public ICollection<BookingPromotion> BookingPromotions { get; set; } = new List<BookingPromotion>();
}
