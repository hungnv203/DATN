namespace MovieBooking.Application.Common.DTOs;

public class PromotionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal MinOrder { get; set; }
    public string Status { get; set; } = string.Empty;
}
