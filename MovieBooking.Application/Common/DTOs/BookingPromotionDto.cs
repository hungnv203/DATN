namespace MovieBooking.Application.Common.DTOs;

public class BookingPromotionDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid PromotionId { get; set; }
    public decimal DiscountAmount { get; set; }
}
